﻿using System  ;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;
using EnvDTE;
using VisualLocalizer.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using System.Text.RegularExpressions;
using VisualLocalizer.Gui;
using EnvDTE80;
using Microsoft.VisualStudio.OLE.Interop;
using VisualLocalizer.Components;
using VisualLocalizer.Library;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace VisualLocalizer.Commands {

    internal sealed class MoveToResourcesCommand : AbstractCommand {

        public override void Process() {
            base.Process();

            CodeStringResultItem resultItem = GetReplaceStringItem();

            if (resultItem != null) {                
                TextSpan replaceSpan = resultItem.ReplaceSpan;
                string referencedCodeText = resultItem.Value;                
             
                SelectResourceFileForm f = new SelectResourceFileForm(
                    referencedCodeText.CreateKeySuggestions(resultItem.NamespaceElement == null ? null : (resultItem.NamespaceElement as CodeNamespace).FullName,
                        resultItem.ClassOrStructElementName, resultItem.MethodElementName == null ? resultItem.VariableElementName : resultItem.MethodElementName),
                    referencedCodeText,
                    currentDocument.ProjectItem.ContainingProject
                );
                System.Windows.Forms.DialogResult result = f.ShowDialog(System.Windows.Forms.Form.FromHandle(new IntPtr(VisualLocalizerPackage.Instance.DTE.MainWindow.HWnd)));

                if (result == System.Windows.Forms.DialogResult.OK) {
                    bool unitsFromStackRemoved = false;
                    bool unitMovedToResource = false;
                    string referenceText;
                    bool addNamespace = false;

                    try {                        
                        if (f.UsingFullName) {
                            referenceText = f.SelectedItem.Namespace + "." + f.SelectedItem.Class + "." + f.Key;
                            addNamespace = false;
                        } else {
                            NamespacesList usedNamespaces = resultItem.NamespaceElement.GetUsedNamespaces(resultItem.SourceItem);
                            addNamespace = usedNamespaces.ResolveNewElement(f.SelectedItem.Namespace, f.SelectedItem.Class, f.Key,
                                currentDocument.ProjectItem.ContainingProject, out referenceText);
                        }

                        int hr = textLines.ReplaceLines(replaceSpan.iStartLine, replaceSpan.iStartIndex, replaceSpan.iEndLine, replaceSpan.iEndIndex,
                            Marshal.StringToBSTR(referenceText), referenceText.Length, new TextSpan[] { replaceSpan });                        
                        Marshal.ThrowExceptionForHR(hr);                        

                        hr = textView.SetSelection(replaceSpan.iStartLine, replaceSpan.iStartIndex,
                            replaceSpan.iStartLine, replaceSpan.iStartIndex + referenceText.Length);
                        Marshal.ThrowExceptionForHR(hr);
                        
                        if (addNamespace) {
                            currentDocument.AddUsingBlock(f.SelectedItem.Namespace);
                        }                        

                        if (f.Result == SELECT_RESOURCE_FILE_RESULT.INLINE) {
                            unitsFromStackRemoved = CreateMoveToResourcesReferenceUndoUnit(f.Key, addNamespace);
                        } else if (f.Result == SELECT_RESOURCE_FILE_RESULT.OVERWRITE) {
                            f.SelectedItem.AddString(f.Key, f.Value);
                            unitMovedToResource = true;                            
                            unitsFromStackRemoved = CreateMoveToResourcesOverwriteUndoUnit(f.Key, f.Value, f.OverwrittenValue, f.SelectedItem, addNamespace);                            
                        } else {
                            f.SelectedItem.AddString(f.Key, f.Value);
                            unitMovedToResource = true;                            
                            unitsFromStackRemoved = CreateMoveToResourcesUndoUnit(f.Key, f.Value, f.SelectedItem, addNamespace);                            
                        }
                
                    } catch (Exception) {
                        VLOutputWindow.VisualLocalizerPane.WriteLine("Exception caught, rolling back...");
                        if (!unitsFromStackRemoved) {
                            List<IOleUndoUnit> units = undoManager.RemoveTopFromUndoStack(addNamespace ? 2 : 1);                            
                            foreach (var unit in units) {
                                unit.Do(undoManager);                                
                            }
                            undoManager.RemoveTopFromUndoStack(units.Count);
                            if (unitMovedToResource) {
                                f.SelectedItem.RemoveKey(f.Key);
                            }
                        } else {
                            AbstractUndoUnit unit = (AbstractUndoUnit)undoManager.RemoveTopFromUndoStack(1)[0];
                            int unitsToRemove = unit.AppendUnits.Count + 1;
                            unit.Do(undoManager);
                            undoManager.RemoveTopFromUndoStack(unitsToRemove);
                        }                        
                        throw;
                    }
                }
            } else throw new Exception("This part of code cannot be referenced");
        }      

        private bool CreateMoveToResourcesUndoUnit(string key,string value, ResXProjectItem resXProjectItem,bool addNamespace) {
            bool unitsRemoved = false;
            List<IOleUndoUnit> units = undoManager.RemoveTopFromUndoStack(addNamespace ? 2 : 1);
            unitsRemoved = true;

            MoveToResourcesUndoUnit newUnit = new MoveToResourcesUndoUnit(key, value, resXProjectItem);
            newUnit.AppendUnits.AddRange(units);
            undoManager.Add(newUnit);

            return unitsRemoved;
        }

        private bool CreateMoveToResourcesOverwriteUndoUnit(string key, string newValue, string oldValue, ResXProjectItem resXProjectItem, bool addNamespace) {
            bool unitsRemoved = false;
            List<IOleUndoUnit> units = undoManager.RemoveTopFromUndoStack(addNamespace ? 2 : 1);
            unitsRemoved = true;

            MoveToResourcesOverwriteUndoUnit newUnit = new MoveToResourcesOverwriteUndoUnit(key, oldValue, newValue, resXProjectItem);
            newUnit.AppendUnits.AddRange(units);
            undoManager.Add(newUnit);

            return unitsRemoved;
        }

        private bool CreateMoveToResourcesReferenceUndoUnit(string key, bool addNamespace) {
            bool unitsRemoved = false;
            List<IOleUndoUnit> units = undoManager.RemoveTopFromUndoStack(addNamespace ? 2 : 1);
            unitsRemoved = true;

            MoveToResourcesReferenceUndoUnit newUnit = new MoveToResourcesReferenceUndoUnit(key);
            newUnit.AppendUnits.AddRange(units);
            undoManager.Add(newUnit);

            return unitsRemoved;
        } 

        private string TrimAtAndApos(string value) {
            if (value.StartsWith("@")) value = value.Substring(1);
            return value.Substring(1, value.Length - 2);
        }

        private CodeStringResultItem GetReplaceStringItem() {
            string text;
            TextPoint startPoint;
            string codeFunctionName;
            string codeVariableName;
            CodeElement2 codeClass;
            TextSpan selectionSpan;
            bool ok = GetCodeBlockFromSelection(out text, out startPoint, out codeFunctionName, out codeVariableName, out codeClass,out selectionSpan);
            CodeStringResultItem result = null;

            if (ok) {
                CodeStringLookuper lookuper = new CodeStringLookuper(text, startPoint,
                    codeClass.GetNamespace(), codeClass.Name, codeFunctionName, codeVariableName, false);
                List<CodeStringResultItem> items = lookuper.LookForStrings();

                foreach (CodeStringResultItem item in items) {
                    if (item.ReplaceSpan.Contains(selectionSpan)) {
                        result = item;
                        result.SourceItem = currentDocument.ProjectItem;
                        break;
                    }
                }
            }

            return result;
             
        }
       
    }
}
