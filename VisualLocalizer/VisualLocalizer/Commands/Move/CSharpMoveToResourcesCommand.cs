﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VisualLocalizer.Components;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.TextManager.Interop;
using VisualLocalizer.Library;

namespace VisualLocalizer.Commands {
    internal sealed class CSharpMoveToResourcesCommand : MoveToResourcesCommand<CSharpStringResultItem> {
              
        protected override CSharpStringResultItem GetReplaceStringItem() {
            if (currentCodeModel == null)
                throw new Exception("Current document has no CodeModel.");

            string text;
            TextPoint startPoint;
            string codeFunctionName;
            string codeVariableName;
            CodeElement2 codeClass;
            TextSpan selectionSpan;
            bool ok = GetCodeBlockFromSelection(out text, out startPoint, out codeFunctionName, out codeVariableName, out codeClass, out selectionSpan);
            CSharpStringResultItem result = null;

            if (ok) {
                List<CSharpStringResultItem> items = CSharpStringLookuper.Instance.Run(currentDocument.ProjectItem, currentDocument.ProjectItem.IsGenerated(), text, startPoint,
                    codeClass.GetNamespace(), codeClass.Name, codeFunctionName, codeVariableName, false);                

                foreach (CSharpStringResultItem item in items) {
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
