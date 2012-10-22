﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE80;
using EnvDTE;
using VisualLocalizer.Components;
using VisualLocalizer.Library;
using VisualLocalizer.Extensions;

namespace VisualLocalizer.Commands {
    internal sealed class BatchInlineCommand : AbstractBatchCommand {

        public List<CodeReferenceResultItem> Results {
            get;
            private set;
        }
        private Dictionary<Project, Trie<CodeReferenceTrieElement>> trieCache = new Dictionary<Project, Trie<CodeReferenceTrieElement>>();
        private Dictionary<CodeElement, NamespacesList> codeUsingsCache = new Dictionary<CodeElement, NamespacesList>();

        public override void Process() {
            base.Process();            

            VLOutputWindow.VisualLocalizerPane.WriteLine("Batch Inline command started on active document... ");
            if (currentlyProcessedItem.Document.ReadOnly)
                throw new Exception("Cannot perform this operation - active document is readonly");

            Results = new List<CodeReferenceResultItem>();

            Process(currentlyProcessedItem);

            Results.ForEach((item) => {
                VLDocumentViewsManager.SetFileReadonly(item.SourceItem.Properties.Item("FullPath").Value.ToString(), true); 
            });

            trieCache.Clear();
            codeUsingsCache.Clear();
            VLOutputWindow.VisualLocalizerPane.WriteLine("Found {0} items to be moved", Results.Count);
        }

        public override void Process(Array selectedItems) {            
            VLOutputWindow.VisualLocalizerPane.WriteLine("Batch Inline command started on selection");
            Results = new List<CodeReferenceResultItem>();

            base.Process(selectedItems);

            Results.ForEach((item) => {
                VLDocumentViewsManager.SetFileReadonly(item.SourceItem.Properties.Item("FullPath").Value.ToString(), true); 
            });

            trieCache.Clear();
            codeUsingsCache.Clear();
            VLOutputWindow.VisualLocalizerPane.WriteLine("Batch Inline completed - found {0} items to be moved", Results.Count);
        }

        public override void ProcessSelection() {
            base.ProcessSelection();

            VLOutputWindow.VisualLocalizerPane.WriteLine("Batch Inline command started on text selection of active document ");

            Results = new List<CodeReferenceResultItem>();

            Process(currentlyProcessedItem, IntersectsWithSelection);

            Results.RemoveAll((item) => {
                bool empty = item.Value.Trim().Length == 0;
                return empty || IsItemOutsideSelection(item);
            });
            Results.ForEach((item) => {
                VLDocumentViewsManager.SetFileReadonly(item.SourceItem.Properties.Item("FullPath").Value.ToString(), true);
            });

            VLOutputWindow.VisualLocalizerPane.WriteLine("Found {0} items to be moved", Results.Count);
        }

        private Trie<CodeReferenceTrieElement> PutResourceFilesInCache() {
            if (!trieCache.ContainsKey(currentlyProcessedItem.ContainingProject)) {
                var resxItems = currentlyProcessedItem.GetResXItemsAround(false);
                trieCache.Add(currentlyProcessedItem.ContainingProject, resxItems.CreateTrie());
            }
            return trieCache[currentlyProcessedItem.ContainingProject]; 
        }

        private NamespacesList PutCodeUsingsInCache(CodeElement parentNamespace, CodeElement codeClassOrStruct) {
            if (parentNamespace == null) {
                if (!codeUsingsCache.ContainsKey(codeClassOrStruct)) {
                    codeUsingsCache.Add(codeClassOrStruct, (null as CodeNamespace).GetUsedNamespaces(currentlyProcessedItem));
                }
                return codeUsingsCache[codeClassOrStruct];
            } else {
                if (!codeUsingsCache.ContainsKey(parentNamespace)) {
                    codeUsingsCache.Add(parentNamespace, (parentNamespace as CodeNamespace).GetUsedNamespaces(currentlyProcessedItem));
                }
                return codeUsingsCache[parentNamespace as CodeElement];
            }            
        }

        protected override void Lookup(string functionText, TextPoint startPoint, CodeNamespace parentNamespace, CodeElement2 codeClassOrStruct, string codeFunctionName, string codeVariableName,bool isWithinLocFalse) {
            Trie<CodeReferenceTrieElement> trie = PutResourceFilesInCache();
            NamespacesList usedNamespaces = PutCodeUsingsInCache(parentNamespace as CodeElement, codeClassOrStruct);

            CodeReferenceLookuper lookuper = new CodeReferenceLookuper(functionText, startPoint,
                trie, usedNamespaces, parentNamespace, isWithinLocFalse, currentlyProcessedItem.ContainingProject);
            lookuper.SourceItem = currentlyProcessedItem;

            var list = lookuper.LookForReferences();
            EditPoint2 editPoint = (EditPoint2)startPoint.CreateEditPoint();
            foreach (var item in list)
                AddContextToItem(item, editPoint);

            Results.AddRange(list);
        }
    }
}
