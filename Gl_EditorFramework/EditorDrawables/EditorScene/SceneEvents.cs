﻿using GL_EditorFramework.GL_Core;
using GL_EditorFramework.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinInput = System.Windows.Input;
using static GL_EditorFramework.EditorDrawables.EditableObject;
using System.Drawing;

namespace GL_EditorFramework.EditorDrawables
{
    public abstract partial class EditorSceneBase : AbstractGlDrawable
    {
        public override uint MouseDown(MouseEventArgs e, GL_ControlBase control)
        {
            uint var = 0;
            if (CurrentAction == NoAction && ExclusiveAction == NoAction && Hovered != null)
            {
                LocalOrientation rLocalOrientation;
                bool rDragExclusively;
                if (e.Button == MouseButtons.Left)
                {
                    if (Hovered.TryStartDragging(DragActionType.TRANSLATE, HoveredPart, out rLocalOrientation, out rDragExclusively))
                    {
                        draggingDepth = control.PickingDepth;
                        if (rDragExclusively)
                            ExclusiveAction = new TranslateAction(control, e.Location, rLocalOrientation.Origin, draggingDepth);
                        else
                        {
                            BoundingBox box = BoundingBox.Default;

                            foreach (IEditableObject selected in SelectedObjects)
                                selected.GetSelectionBox(ref box);

                            CurrentAction = new TranslateAction(control, e.Location, box.GetCenter(), draggingDepth);
                        }
                    }
                }
                else if (e.Button == MouseButtons.Right)
                {
                    if (Hovered.TryStartDragging(DragActionType.ROTATE, HoveredPart, out rLocalOrientation, out rDragExclusively))
                    {
                        draggingDepth = control.PickingDepth;
                        if (rDragExclusively)
                            ExclusiveAction = new RotateAction(control, e.Location, rLocalOrientation.Origin, draggingDepth);
                        else
                        {
                            BoundingBox box = BoundingBox.Default;

                            foreach (IEditableObject selected in SelectedObjects)
                                selected.GetSelectionBox(ref box);

                            CurrentAction = new RotateAction(control, e.Location, box.GetCenter(), draggingDepth);
                        }
                    }
                }
                else if (e.Button == MouseButtons.Middle)
                {
                    bool shift = WinInput.Keyboard.IsKeyDown(WinInput.Key.LeftShift);
                    if (Hovered.TryStartDragging(shift ? DragActionType.SCALE : DragActionType.SCALE_EXCLUSIVE, HoveredPart, out rLocalOrientation, out rDragExclusively))
                    {
                        draggingDepth = control.PickingDepth;
                        if (rDragExclusively)
                        {
                            if (shift)
                                ExclusiveAction = new ScaleAction(control, e.Location, rLocalOrientation.Origin);
                            else
                                ExclusiveAction = new ScaleActionIndividual(control, e.Location, rLocalOrientation);
                        }
                        else
                        {
                            BoundingBox box = BoundingBox.Default;

                            foreach (IEditableObject selected in SelectedObjects)
                                selected.GetSelectionBox(ref box);

                            if (shift)
                                CurrentAction = new ScaleAction(control, e.Location, box.GetCenter());
                            else
                                CurrentAction = new ScaleActionIndividual(control, e.Location, rLocalOrientation);
                        }
                    }
                }
            }
            else
            {
                var |= REDRAW_PICKING;
                var |= FORCE_REENTER;

                CurrentAction = NoAction; //abort current action
                ExclusiveAction = NoAction;
            }

            foreach (AbstractGlDrawable obj in staticObjects)
            {
                var |= obj.MouseDown(e, control);
            }

            foreach (IEditableObject obj in GetObjects())
            {
                var |= obj.MouseDown(e, control);
            }

            return var;
        }

        public override uint MouseMove(MouseEventArgs e, Point lastMousePos, GL_ControlBase control)
        {
            uint var = 0;

            foreach (IEditableObject obj in GetObjects())
            {
                var |= obj.MouseMove(e, lastMousePos, control);
            }

            foreach (AbstractGlDrawable obj in staticObjects)
            {
                var |= obj.MouseMove(e, lastMousePos, control);
            }

            if (CurrentAction != NoAction || ExclusiveAction != NoAction)
            {
                CurrentAction.UpdateMousePos(e.Location);
                ExclusiveAction.UpdateMousePos(e.Location);

                var |= REDRAW | NO_CAMERA_ACTION;

                var &= ~REPICK;
            }
            else
            {
                var |= REPICK;
            }
            return var;
        }

        public override uint MouseWheel(MouseEventArgs e, GL_ControlBase control)
        {
            uint var = 0;
            foreach (IEditableObject obj in GetObjects())
            {
                var |= obj.MouseWheel(e, control);
            }

            foreach (AbstractGlDrawable obj in staticObjects)
            {
                var |= obj.MouseWheel(e, control);
            }

            if (CurrentAction != NoAction)
            {
                CurrentAction.ApplyScrolling(e.Location, e.Delta);
                ExclusiveAction.ApplyScrolling(e.Location, e.Delta);

                var |= REDRAW | NO_CAMERA_ACTION;

                var &= ~REPICK;
            }

            return var;
        }

        public override uint MouseUp(MouseEventArgs e, GL_ControlBase control)
        {
            TransformChangeInfos transformChangeInfos = new TransformChangeInfos(new List<TransformChangeInfo>());
            uint var = 0;

            if (CurrentAction != NoAction && CurrentAction.IsApplyOnRelease())
            {
                foreach (IEditableObject obj in SelectedObjects)
                {
                    obj.ApplyTransformActionToSelection(CurrentAction, ref transformChangeInfos);
                }

                var |= REDRAW_PICKING | FORCE_REENTER;

                CurrentAction = NoAction;
            }

            if (ExclusiveAction != NoAction)
            {
                Hovered.ApplyTransformActionToPart(ExclusiveAction, HoveredPart, ref transformChangeInfos);

                var |= REDRAW_PICKING;

                ExclusiveAction = NoAction;
            }

            foreach (AbstractGlDrawable obj in staticObjects)
            {
                var |= obj.MouseUp(e, control);
            }

            AddTransformToUndo(transformChangeInfos);

            foreach (IEditableObject obj in GetObjects())
            {
                var |= obj.MouseUp(e, control);
            }

            return var;
        }

        public override uint MouseClick(MouseEventArgs e, GL_ControlBase control)
        {
            uint var = 0;
            foreach (IEditableObject obj in GetObjects())
            {
                var |= obj.MouseClick(e, control);
            }

            foreach (AbstractGlDrawable obj in staticObjects)
            {
                var |= obj.MouseClick(e, control);
            }

            bool shift = WinInput.Keyboard.IsKeyDown(WinInput.Key.LeftShift);
            bool hoveredIsSelected = Hovered != null && Hovered.IsSelected(HoveredPart);
            bool nothingHovered = Hovered == null;

            if (e.Button == MouseButtons.Left)
            {
                if (nothingHovered)
                {
                    if (!shift)
                    {
                        foreach (IEditableObject selected in SelectedObjects)
                        {
                            selected.DeselectAll(control, null);
                        }
                        SelectedObjects.Clear();
                        SelectionChanged?.Invoke(this, new EventArgs());
                    }
                }
                else if (multiSelect)
                {
                    if (shift && hoveredIsSelected)
                    {
                        //remove from selection
                        Hovered.Deselect(HoveredPart, control, SelectedObjects);
                        SelectionChanged?.Invoke(this, new EventArgs());
                    }
                    else if (shift)
                    {
                        //add to selection
                        Hovered.Select(HoveredPart, control, SelectedObjects);
                        SelectionChanged?.Invoke(this, new EventArgs());
                    }
                    else if (!hoveredIsSelected)
                    {
                        //change selection
                        foreach (IEditableObject selected in SelectedObjects)
                        {
                            selected.DeselectAll(control, null);
                        }
                        SelectedObjects.Clear();
                        Hovered.Select(HoveredPart, control, SelectedObjects);
                        SelectionChanged?.Invoke(this, new EventArgs());
                    }
                }
                else
                {
                    if (shift && hoveredIsSelected)
                    {
                        //remove from selection
                        Hovered.Deselect(HoveredPart, control, SelectedObjects);
                        SelectionChanged?.Invoke(this, new EventArgs());
                    }
                    else if (!hoveredIsSelected)
                    {
                        //change selection
                        foreach (IEditableObject selected in SelectedObjects)
                        {
                            selected.DeselectAll(control, null);
                        }
                        SelectedObjects.Clear();
                        Hovered.Select(HoveredPart, control, SelectedObjects);
                        SelectionChanged?.Invoke(this, new EventArgs());
                    }
                }
            }

            CurrentAction = NoAction; //because MouseClick implies that the Mouse Button is not pressed anymore

            var |= REDRAW;
            var |= FORCE_REENTER;

            return var;
        }

        public override uint MouseEnter(int inObjectIndex, GL_ControlBase control)
        {
            if (CurrentAction != NoAction || ExclusiveAction != NoAction)
                return 0;

            foreach (IEditableObject obj in GetObjects())
            {
                if (!(obj.Visible && obj.IsInRange(renderDistance, renderDistanceSquared, control.CameraPosition)))
                    continue;
                int span = obj.GetPickableSpan();
                if (inObjectIndex >= 0 && inObjectIndex < span)
                {
                    Hovered = obj;
                    HoveredPart = inObjectIndex;
                    return obj.MouseEnter(inObjectIndex, control) | REDRAW;
                }
                inObjectIndex -= span;
            }

            foreach (AbstractGlDrawable obj in staticObjects)
            {
                int span = obj.GetPickableSpan();
                if (inObjectIndex >= 0 && inObjectIndex < span)
                {
                    return obj.MouseEnter(inObjectIndex, control);
                }
                inObjectIndex -= span;
            }
            return 0;
        }

        public override uint MouseLeave(int inObjectIndex, GL_ControlBase control)
        {
            if (CurrentAction != NoAction || ExclusiveAction != NoAction)
                return 0;
            foreach (IEditableObject obj in GetObjects())
            {
                if (!(obj.Visible && obj.IsInRange(renderDistance, renderDistanceSquared, control.CameraPosition)))
                    continue;
                int span = obj.GetPickableSpan();
                if (inObjectIndex >= 0 && inObjectIndex < span)
                {
                    return obj.MouseLeave(inObjectIndex, control);
                }
                inObjectIndex -= span;
            }

            foreach (AbstractGlDrawable obj in staticObjects)
            {
                int span = obj.GetPickableSpan();
                if (inObjectIndex >= 0 && inObjectIndex < span)
                {
                    return obj.MouseLeave(inObjectIndex, control);
                }
                inObjectIndex -= span;
            }
            return 0;
        }

        public override uint MouseLeaveEntirely(GL_ControlBase control)
        {
            if (CurrentAction != NoAction || ExclusiveAction != NoAction)
                return 0;

            Hovered = null;
            return REDRAW;
        }

        public override uint KeyDown(KeyEventArgs e, GL_ControlBase control)
        {
            TransformChangeInfos transformChangeInfos = new TransformChangeInfos(new List<TransformChangeInfo>());
            uint var = 0;

            bool selectionHasChanged = false;

            if (CurrentAction != NoAction || ExclusiveAction != NoAction)
            {
                CurrentAction.KeyDown(e);
                ExclusiveAction.KeyDown(e);
                var = NO_CAMERA_ACTION | REDRAW;
            }
            else if (e.KeyCode == Keys.Z) //focus camera on the selection
            {
                if (e.Control)
                {
                    Undo();
                }
                else if (SelectedObjects.Count > 0)
                {
                    EditableObject.BoundingBox box = EditableObject.BoundingBox.Default;

                    foreach (IEditableObject selected in SelectedObjects)
                    {
                        selected.GetSelectionBox(ref box);
                    }
                    control.CameraTarget = box.GetCenter();
                }

                var = REDRAW_PICKING;
            }
            else if (e.KeyCode == Keys.Y && e.Control)
            {
                Redo();

                var = REDRAW_PICKING;
            }
            else if (e.KeyCode == Keys.H && SelectedObjects.Count > 0) //hide/show selected objects
            {
                foreach (IEditableObject selected in SelectedObjects)
                {
                    selected.Visible = e.Shift;
                }
                var = REDRAW_PICKING;
            }
            else if (e.KeyCode == Keys.S && SelectedObjects.Count > 0 && e.Shift) //auto snap selected objects
            {
                SnapAction action = new SnapAction();
                foreach (IEditableObject selected in SelectedObjects)
                {
                    selected.ApplyTransformActionToSelection(action, ref transformChangeInfos);
                }
                var = REDRAW_PICKING;
            }
            else if (e.KeyCode == Keys.R && SelectedObjects.Count > 0 && e.Shift && e.Control) //reset scale for selected objects
            {
                ResetScale action = new ResetScale();
                foreach (IEditableObject selected in SelectedObjects)
                {
                    selected.ApplyTransformActionToSelection(action, ref transformChangeInfos);
                }
                var = REDRAW_PICKING;
            }
            else if (e.KeyCode == Keys.R && SelectedObjects.Count > 0 && e.Shift) //reset rotation for selected objects
            {
                ResetRot action = new ResetRot();
                foreach (IEditableObject selected in SelectedObjects)
                {
                    selected.ApplyTransformActionToSelection(action, ref transformChangeInfos);
                }
                var = REDRAW_PICKING;
            }
            else if (e.Control && e.KeyCode == Keys.A) //select/deselect all objects
            {
                if (e.Shift && SelectedObjects.Count > 0)
                {
                    foreach (IEditableObject selected in SelectedObjects)
                    {
                        selected.DeselectAll(control, null);
                    }
                    SelectedObjects.Clear();
                    selectionHasChanged = true;
                }

                if (!e.Shift && multiSelect)
                {
                    foreach (IEditableObject obj in GetObjects())
                    {
                        obj.SelectAll(control, SelectedObjects);
                    }
                    selectionHasChanged = true;
                }
                var = REDRAW;
            }

            foreach (IEditableObject obj in GetObjects())
            {
                var |= obj.KeyDown(e, control);
            }
            foreach (AbstractGlDrawable obj in staticObjects)
            {
                var |= obj.KeyDown(e, control);
            }
            if (selectionHasChanged)
                UpdateSelection(var);

            AddTransformToUndo(transformChangeInfos);

            return var;
        }

        public override uint KeyUp(KeyEventArgs e, GL_ControlBase control)
        {
            uint var = 0;
            foreach (IEditableObject obj in GetObjects())
            {
                var |= obj.KeyUp(e, control);
            }
            foreach (AbstractGlDrawable obj in staticObjects)
            {
                var |= obj.KeyUp(e, control);
            }
            return var;
        }
    }
}