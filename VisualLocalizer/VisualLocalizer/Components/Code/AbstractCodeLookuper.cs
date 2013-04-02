﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using VisualLocalizer.Library;
using VisualLocalizer.Library.AspxParser;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VisualLocalizer.Components {

    /// <summary>
    /// Provides functionality for looking up objects in C# or VB code text. "Object" can be either string literal or reference to
    /// a resource. One instance of such object is called "result item" and holds all necessary information like position,
    /// value and more. Descendants of this class are implemented as singletons. 
    /// </summary>
    /// <typeparam name="T">Type of result items class</typeparam>
    internal abstract class AbstractCodeLookuper<T> where T:AbstractResultItem,new() {

        /// <summary>
        /// Corresponding ProjectItem, in which given code is located
        /// </summary>
        protected ProjectItem SourceItem { get; set; }

        /// <summary>
        /// True if SourceItem is generated by some CustomTool (is designer file)
        /// </summary>
        protected bool SourceItemGenerated { get; set; }

        /// <summary>
        /// Current lookuper position - line
        /// </summary>
        protected int CurrentLine { get; set; }

        /// <summary>
        /// Current lookuper position - column
        /// </summary>
        protected int CurrentIndex { get; set; }

        /// <summary>
        /// Current lookuper position - number of characters from the beginning of the text
        /// </summary>
        protected int CurrentAbsoluteOffset { get; set; }

        /// <summary>
        /// Starting lookuper position - line
        /// </summary>
        protected int OriginalLine { get; set; }

        /// <summary>
        /// Starting lookuper position - column
        /// </summary>
        protected int OriginalIndex { get; set; }

        /// <summary>
        /// Starting lookuper position - number of characters from the beginning of the text
        /// </summary>
        protected int OriginalAbsoluteOffset { get; set; }

        /// <summary>
        /// Last string position - line
        /// </summary>
        protected int StringStartLine { get; set; }

        /// <summary>
        /// Last string position - column
        /// </summary>
        protected int StringStartIndex { get; set; }

        /// <summary>
        /// Last string position - number of characters from the beginning of the text
        /// </summary>
        protected int StringStartAbsoluteOffset { get; set; }

        /// <summary>
        /// Last reference position - line
        /// </summary>
        protected int ReferenceStartLine { get; set; }

        /// <summary>
        /// Last reference position - column
        /// </summary>
        protected int ReferenceStartIndex { get; set; }

        /// <summary>
        /// Last reference position - number of characters from the beginning of the text
        /// </summary>
        protected int ReferenceStartOffset { get; set; }

        /// <summary>
        /// True if current block of code is decorated with [Localizable(false)]
        /// </summary>
        protected bool IsWithinLocFalse { get; set; }        

        /// <summary>
        /// Code text to be searched
        /// </summary>
        protected string text;

        /// <summary>
        /// Current lookuper position - currently processed character
        /// </summary>
        protected char currentChar;
        
        /// <summary>
        /// Character which last string result started with (" or ')
        /// </summary>
        protected char stringStartChar;        

        /// <summary>
        /// Index of current char in code text
        /// </summary>
        protected int globalIndex;        

        /// <summary>
        /// Project in which code text belongs
        /// </summary>
        protected Project Project { get; set; }

        /// <summary>
        /// Namespaces affecting code text
        /// </summary>
        protected NamespacesList UsedNamespaces { get; set; }

        /// <summary>
        /// Trie used to lookup references
        /// </summary>
        protected Trie<CodeReferenceTrieElement> Trie { get; set; }  
      
        protected ResXProjectItem prefferedResXItem;
        private object syncRoot = new object();

        /// <summary>
        /// Language-specific implementation, handles beginnings and ends of strings, comments etc.
        /// </summary>
        /// <param name="insideComment">IN/OUT - true if lookuper's position is within comment</param>
        /// <param name="insideString">IN/OUT - true if lookuper's position is within string literal</param>
        /// <param name="isVerbatimString">IN/OUT - true string literal is verbatim (C# only)</param>
        /// <param name="skipLine">OUT - true if lookuper should skip current line entirely</param>
        protected abstract void PreProcessChar(ref bool insideComment, ref bool insideString, ref bool isVerbatimString, out bool skipLine);

        /// <summary>
        /// Returns list of string literals result items in given block of code
        /// </summary>
        /// <param name="projectItem">Project item where code belongs</param>
        /// <param name="isGenerated">Whether project item is designer file</param>
        /// <param name="text">Text to search</param>
        /// <param name="startPoint">Position of the text in its source file</param>
        /// <param name="isWithinLocFalse">True if code block is decorated with [Localizable(false)]</param>        
        protected List<T> LookForStrings(ProjectItem projectItem, bool isGenerated, string text, TextPoint startPoint, bool isWithinLocFalse) {
            if (projectItem == null) throw new ArgumentNullException("projectItem");
            if (text == null) throw new ArgumentNullException("text");
            if (startPoint == null) throw new ArgumentNullException("startPoint");

            lock (syncRoot) {
                this.SourceItemGenerated = isGenerated;
                this.SourceItem = projectItem;
                this.text = text;
                this.CurrentIndex = startPoint.LineCharOffset - 1;
                this.CurrentLine = startPoint.Line;
                this.CurrentAbsoluteOffset = startPoint.AbsoluteCharOffset + startPoint.Line - 2;
                this.IsWithinLocFalse = isWithinLocFalse;
                this.OriginalAbsoluteOffset = this.CurrentAbsoluteOffset;
                this.OriginalLine = this.CurrentLine;
                this.OriginalIndex = this.CurrentIndex;

                return LookForStrings();
            }
        }

        /// <summary>
        /// Returns list of string literals result items in given block of code
        /// </summary>
        /// <param name="projectItem">Project item where code belongs</param>
        /// <param name="isGenerated">Whether project item is designer file</param>
        /// <param name="text">Text to search</param>
        /// <param name="blockSpan">Position of the text in its source file</param>
        protected List<T> LookForStrings(ProjectItem projectItem, bool isGenerated, string text, BlockSpan blockSpan) {
            if (projectItem == null) throw new ArgumentNullException("projectItem");
            if (text == null) throw new ArgumentNullException("text");
            if (blockSpan == null) throw new ArgumentNullException("blockSpan");

            lock (syncRoot) {
                this.SourceItemGenerated = isGenerated;
                this.SourceItem = projectItem;
                this.text = text;
                this.CurrentIndex = blockSpan.StartIndex - 1;
                this.CurrentLine = blockSpan.StartLine;
                this.CurrentAbsoluteOffset = blockSpan.AbsoluteCharOffset;
                this.IsWithinLocFalse = false;
                this.OriginalAbsoluteOffset = this.CurrentAbsoluteOffset;
                this.OriginalLine = this.CurrentLine;
                this.OriginalIndex = this.CurrentIndex;

                return LookForStrings();
            }
        }

        /// <summary>
        /// Returns list of references to resource within given block of code
        /// </summary>
        /// <param name="projectItem">Project item where code belongs</param>        
        /// <param name="text">Text to search</param>
        /// <param name="startPoint">Position of the text in its source file</param>        
        /// <param name="Trie">Trie consisting of searched references</param>
        /// <param name="usedNamespaces">Namespaces affecting the block of code</param>
        /// <param name="isWithinLocFalse">True if code block is decorated with [Localizable(false)]</param> 
        /// <param name="project">Project in which the code belongs</param>
        /// <param name="prefferedResXItem"></param>        
        public List<T> LookForReferences(ProjectItem projectItem, string text, TextPoint startPoint, Trie<CodeReferenceTrieElement> Trie, NamespacesList usedNamespaces, bool isWithinLocFalse, Project project, ResXProjectItem prefferedResXItem) {
            return LookForReferences(projectItem, text, startPoint.LineCharOffset - 1, startPoint.Line, startPoint.AbsoluteCharOffset + startPoint.Line - 2, Trie, usedNamespaces, isWithinLocFalse, project, prefferedResXItem);
        }

        /// <summary>
        /// Returns list of references to resource within given block of code
        /// </summary>
        /// <param name="projectItem">Project item where code belongs</param>
        /// <param name="text">Text to search</param>
        /// <param name="blockSpan">Position of the text in its source file</param>
        /// <param name="Trie">Trie consisting of searched references</param>
        /// <param name="usedNamespaces">Namespaces affecting the block of code</param>
        /// <param name="project">Project in which the code belongs</param>
        /// <param name="prefferedResXItem"></param>
        /// <returns></returns>
        public List<T> LookForReferences(ProjectItem projectItem, string text, BlockSpan blockSpan, Trie<CodeReferenceTrieElement> Trie, NamespacesList usedNamespaces, Project project, ResXProjectItem prefferedResXItem) {
            return LookForReferences(projectItem, text, blockSpan.StartIndex - 1, blockSpan.StartLine, blockSpan.AbsoluteCharOffset, Trie, usedNamespaces, false, project, prefferedResXItem);
        }

        /// <summary>
        /// Returns list of references to resource within given block of code
        /// </summary>
        /// <param name="projectItem">Project item where code belongs</param>
        /// <param name="text">Text to search</param>
        /// <param name="currentIndex">Position of the code block in its source file - column</param>
        /// <param name="currentLine">Position of the code block in its source file - line</param>
        /// <param name="currentOffset">Position of the code block in its source file - char offset</param>
        /// <param name="Trie">Trie consisting of searched references</param>
        /// <param name="usedNamespaces">Namespaces affecting the block of code</param>
        /// <param name="isWithinLocFalse">True if code block is decorated with [Localizable(false)]</param> 
        /// <param name="project">Project in which the code belongs</param>
        /// <param name="prefferedResXItem"></param>
        /// <returns></returns>
        public List<T> LookForReferences(ProjectItem projectItem, string text, int currentIndex, int currentLine, int currentOffset,
            Trie<CodeReferenceTrieElement> Trie, NamespacesList usedNamespaces, bool isWithinLocFalse, Project project, ResXProjectItem prefferedResXItem) {
            lock (syncRoot) {
                this.SourceItem = projectItem;
                this.text = text;
                this.CurrentIndex = currentIndex;
                this.CurrentLine = currentLine;
                this.CurrentAbsoluteOffset = currentOffset;
                this.Trie = Trie;
                this.UsedNamespaces = usedNamespaces;
                this.IsWithinLocFalse = isWithinLocFalse;
                this.Project = project;
                this.prefferedResXItem = prefferedResXItem;
                this.OriginalAbsoluteOffset = this.CurrentAbsoluteOffset;
                this.OriginalLine = this.CurrentLine;
                this.OriginalIndex = this.CurrentIndex;

                return LookForReferences();
            }
        }

        /// <summary>
        /// Moves lookuper position by one forward
        /// </summary>
        protected void Move() {
            CurrentIndex++;
            CurrentAbsoluteOffset++;
            if ((currentChar == '\n')) {
                CurrentIndex = 0;
                CurrentLine++;
            }
        }        
        
        /// <summary>
        /// Adds string literal to the list of results
        /// </summary>
        /// <param name="list">List of results in which it gets added</param>
        /// <param name="originalValue">String literal, including quotes</param>
        /// <param name="isVerbatimString">True if string was verbatim</param>
        /// <param name="isUnlocalizableCommented">True if there was "no-localization" comment</param>
        /// <returns>New result item</returns>
        protected virtual T AddStringResult(List<T> list, string originalValue, bool isVerbatimString, bool isUnlocalizableCommented) {
            string value = originalValue;            
            value = value.Substring(1, value.Length - 2); // trim ""

            // calculate position of the result item
            TextSpan span = new TextSpan();
            span.iStartLine = StringStartLine - 1;
            span.iStartIndex = StringStartIndex;
            span.iEndLine = CurrentLine - 1;
            span.iEndIndex = CurrentIndex + 1;

            // generate result item
            var resultItem = new T();
            resultItem.Value = value;
            resultItem.SourceItem = this.SourceItem;
            resultItem.ComesFromDesignerFile = this.SourceItemGenerated;
            resultItem.ReplaceSpan = span;
            resultItem.AbsoluteCharOffset = StringStartAbsoluteOffset - (isVerbatimString ? 1 : 0);
            resultItem.AbsoluteCharLength = originalValue.Length + (isVerbatimString ? 1 : 0);            
            resultItem.IsWithinLocalizableFalse = IsWithinLocFalse;
            resultItem.IsMarkedWithUnlocalizableComment = isUnlocalizableCommented;

            list.Add(resultItem); // add it to the list

            return resultItem; // and return it
        }

        /// <summary>
        /// Attempts to determine which resource key the reference points to
        /// </summary>        
        protected abstract CodeReferenceInfo ResolveReference(string prefix, string className, List<CodeReferenceInfo> trieElementInfos);
        
        protected CodeReferenceInfo TryResolve(string prefix, string className, List<CodeReferenceInfo> trieElementInfos) {
            CodeReferenceInfo info = null;

            if (string.IsNullOrEmpty(prefix)) { // no namespace
                // get namespace where this class belongs
                UsedNamespaceItem item = UsedNamespaces.ResolveNewReference(className, Project);

                if (item != null) { // namespace found
                    info = GetInfoWithNamespace(trieElementInfos, item.Namespace);
                }
            } else {
                string aliasNamespace = UsedNamespaces.GetNamespace(prefix); // suppose prefix is alias - try get actual namespace
                if (!string.IsNullOrEmpty(aliasNamespace)) { // really, it was just alias
                    info = GetInfoWithNamespace(trieElementInfos, aliasNamespace);
                } else { // no, it was full name of namespace
                    info = GetInfoWithNamespace(trieElementInfos, prefix);

                    if (info == null) { // try adding prefix to the class name - in case prefix is just part of namespace
                        UsedNamespaceItem item = UsedNamespaces.ResolveNewReference(prefix + "." + className, Project);
                        if (item != null) {
                            info = GetInfoWithNamespace(trieElementInfos, item.Namespace + "." + prefix);
                        }
                    }
                }
            }
            return info;
        }

        /// <summary>
        /// Adds reference to a resource result item to the list
        /// </summary>
        /// <param name="list">Result list</param>
        /// <param name="referenceText">Full text of the reference</param>
        /// <param name="trieElementInfos">Info about reference, taken from terminal state of the trie</param>
        /// <returns>New result item</returns>
        protected virtual T AddReferenceResult(List<T> list, string referenceText, List<CodeReferenceInfo> trieElementInfos) {            
            string[] t = referenceText.Split('.');
            if (t.Length < 2) throw new Exception("Code parse error - invalid token " + referenceText);
            string referenceClass;
            string prefix;
            string key;

            if (t.Length == 2) { // reference has format Class.key
                key = t[1]; // get key
                referenceClass = t[0]; // get class
                prefix = null; // no preceding namespace
            } else { // reference looks like Namespace.Class.key
                key = t[t.Length - 1]; // get key
                referenceClass = t[t.Length - 2]; // get class
                prefix = string.Join(".", t, 0, t.Length - 2); // the rest is namespace
            }

            CodeReferenceInfo info = ResolveReference(prefix, referenceClass, trieElementInfos);

            if (info != null) {
                // calculate position of the reference
                TextSpan span = new TextSpan();
                span.iStartLine = ReferenceStartLine - 1;
                span.iStartIndex = ReferenceStartIndex;
                span.iEndLine = CurrentLine - 1;
                span.iEndIndex = CurrentIndex + 1;

                // generate new result item
                var resultItem = new T();
                resultItem.Value = info.Value;
                resultItem.SourceItem = this.SourceItem;
                resultItem.ReplaceSpan = span;
                resultItem.AbsoluteCharOffset = ReferenceStartOffset;
                resultItem.AbsoluteCharLength = CurrentAbsoluteOffset - ReferenceStartOffset + 1;
                resultItem.DestinationItem = info.Origin;
                resultItem.IsWithinLocalizableFalse = IsWithinLocFalse;
                resultItem.Key = info.Key;

                CodeReferenceResultItem refItem = (resultItem as CodeReferenceResultItem);
                refItem.FullReferenceText = string.Format("{0}.{1}.{2}", info.Origin.Namespace, info.Origin.Class, key);
                if (string.IsNullOrEmpty(prefix)) {
                    refItem.OriginalReferenceText = string.Format("{0}.{1}", referenceClass, key);
                } else {
                    refItem.OriginalReferenceText = string.Format("{0}.{1}.{2}", prefix, referenceClass, key);
                }

                list.Add(resultItem);

                return resultItem;
            } else return null;
        }

        protected virtual bool UnderscoreIsLineJoiningChar {
            get {
                return false;
            }
        }

        /// <summary>
        /// Returns list of string literals in current block of code
        /// </summary>        
        protected List<T> LookForStrings() {
            bool insideComment = false, insideString = false, isVerbatimString = false;
            bool skipLine = false;
            currentChar = '?';            
            stringStartChar = '?';
            List<T> list = new List<T>();

            StringBuilder builder = null;
            StringBuilder commentBuilder = null;
            bool lastCommentUnlocalizable = false;
            bool stringMarkedUnlocalized = false;

            for (globalIndex = 0; globalIndex < text.Length; globalIndex++) {
                currentChar = text[globalIndex];

                if (skipLine) { // read until end of line
                    if (currentChar == '\n') {
                        skipLine = false;
                    }
                } else {
                    // language-specific info
                    PreProcessChar(ref insideComment, ref insideString, ref isVerbatimString, out skipLine);

                    bool unlocalizableJustSet = false;
                    if (insideComment) { // inside comment - read to specific buffer (looking for no-localization comment)
                        if (commentBuilder == null) commentBuilder = new StringBuilder();
                        commentBuilder.Append(currentChar);
                    } else if (commentBuilder != null) { // comment terminated
                        lastCommentUnlocalizable = "/" + commentBuilder.ToString() + "/" == StringConstants.CSharpLocalizationComment;
                        commentBuilder = null;
                        unlocalizableJustSet = true;
                    }

                    if (insideString && !insideComment) {
                        if (builder == null) {
                            builder = new StringBuilder();
                            stringMarkedUnlocalized = lastCommentUnlocalizable;
                        }
                        builder.Append(currentChar); // add to string builder
                    } else if (builder != null) {
                        builder.Append(currentChar);
                        if (isVerbatimString) {                                                   
                            builder.Insert(0, '@');
                        }
                        // if it is string, report result
                        if (stringStartChar == '"')
                            AddStringResult(list, builder.ToString(), isVerbatimString, stringMarkedUnlocalized);

                        stringMarkedUnlocalized = false;
                        isVerbatimString = false;
                        builder = null;
                    }

                    if (!unlocalizableJustSet && !char.IsWhiteSpace(currentChar)) lastCommentUnlocalizable = false;
                }

                Move();
            }

            return list;
        }        
        
        /// <summary>
        /// Returns list of references in given block of code
        /// </summary>        
        protected List<T> LookForReferences() {
            bool insideComment = false, insideString = false, isVerbatimString = false;
            bool skipLine = false, toRestore = false;
            CodeReferenceTrieElement previousState = null, prevPrevState = null, cachedState = null;
            currentChar = '?';
            stringStartChar = '?';            
            List<T> list = new List<T>();
            CodeReferenceTrieElement currentElement = Trie.Root;
            StringBuilder prefixBuilder = new StringBuilder();
            StringBuilder previousBuilder = new StringBuilder();
            StringBuilder prevPrevBuilder = new StringBuilder();
            StringBuilder cachedBuilder = new StringBuilder();
            bool prefixContinue = false;
            bool continueLine = false;

            for (globalIndex = 0; globalIndex < text.Length; globalIndex++) {
                currentChar = text[globalIndex];

                if (skipLine) { // read until end of line
                    if (currentChar == '\n') {
                        skipLine = false;                        
                        cachedState = Trie.Root;
                    }
                } else {
                    bool oldInsideComment = insideComment;
                    PreProcessChar(ref insideComment, ref insideString, ref isVerbatimString, out skipLine);

                    if (!insideString && !insideComment) {
                        if (toRestore) {
                            currentElement = cachedState;
                            prefixBuilder = cachedBuilder;
                            toRestore = false;
                        }
                        if (oldInsideComment && cachedState != null && cachedState != Trie.Root) {
                            toRestore = true;
                        }

                        prevPrevBuilder.Length = 0;
                        prevPrevBuilder.Append(previousBuilder);

                        previousBuilder.Length = 0;
                        previousBuilder.Append(prefixBuilder);
                        
                        if (prefixBuilder.Length > 0) {
                            if (currentChar == '.') {
                                prefixContinue = true;
                            } else if (UnderscoreIsLineJoiningChar && currentChar == '_' && char.IsWhiteSpace(GetCharBack(1)) && GetCharBack(-1) == '\r') {
                                continueLine = true;
                                prefixContinue = true;
                            } else if (!currentChar.CanBePartOfIdentifier() && !char.IsWhiteSpace(currentChar)) {
                                prefixBuilder.Length = 0;
                            }
                        }

                        if (currentChar.CanBePartOfIdentifier() && !GetCharBack(1).CanBePartOfIdentifier()) {
                            if (!prefixContinue && !continueLine) {
                                ReferenceStartIndex = CurrentIndex;
                                ReferenceStartLine = CurrentLine;
                                ReferenceStartOffset = CurrentAbsoluteOffset;
                                prefixBuilder.Length = 0;                                
                            }
                            prefixContinue = false;
                        }

                        if (GetCharBack(1) == '\n' && GetCharBack(2) == '\r') {
                            if (continueLine) prefixContinue = true;
                            continueLine = false;
                        }

                        if (!continueLine) {
                            if (currentChar.CanBePartOfIdentifier() || currentChar == '.') {
                                prefixBuilder.Append(currentChar);
                            }

                            prevPrevState = previousState;
                            previousState = currentElement;

                            // use trie to get next state                            
                            currentElement = Trie.Step(currentElement, currentChar);

                            if (currentElement.IsTerminal && !GetCharBack(-1).CanBePartOfIdentifier()) {
                                AddReferenceResult(list, prefixBuilder.ToString(), currentElement.Infos);
                            }
                        }
                    } else {
                        if (!oldInsideComment && insideComment) {
                            cachedState = prevPrevState;
                            cachedBuilder.Length = 0;
                            cachedBuilder.Append(prevPrevBuilder);
                        }
                        currentElement = Trie.Root;
                    }
                    if (!insideString || insideComment) {
                        isVerbatimString = false;
                    }
                }

                Move();
            }

            return list;
        }

        /// <summary>
        /// Returns char 'k' positions back
        /// </summary>        
        protected char GetCharBack(int k) {
            if (globalIndex - k >= 0 && globalIndex - k < text.Length) {
                return text[globalIndex - k];
            } else {
                return '?';
            }
        }

        /// <summary>
        /// Returns number of continuous occurences of given character
        /// </summary>        
        protected int CountBack(char c, int k) {
            k--;
            int count = 0;
            while (k >= 0 && text[k] == c) {
                count++;
                k--;
            }
            return count;
        }

        /// <summary>
        /// Selects that code reference from given list of options, that best matches given namespace.
        /// </summary>        
        protected CodeReferenceInfo GetInfoWithNamespace(List<CodeReferenceInfo> list, string nmspc) {
            CodeReferenceInfo nfo = null;
            foreach (var item in list)
                if (item.Origin.Namespace == nmspc) {
                    if (prefferedResXItem != null) {
                        if (nfo == null || prefferedResXItem == item.Origin) {
                            nfo = item;
                        }
                    } else {
                        if (nfo == null || nfo.Origin.IsCultureSpecific()) {
                            nfo = item;
                        }
                    }

                }
            return nfo;
        }

    }
}
