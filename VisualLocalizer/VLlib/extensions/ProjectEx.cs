﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using VSLangProj;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace VisualLocalizer.Library {
    public static class ProjectEx {

        private const string GlobalWebSiteResourcesFolder = "App_GlobalResources";
        private const string WebSiteProject = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";

        public static List<ProjectItem> GetFiles(this Project project,Predicate<ProjectItem> test,bool includeReferenced) {
            if (project == null)
                throw new ArgumentNullException("project");
            
            List<ProjectItem> list = new List<ProjectItem>();            

            List<ProjectItem> ownFiles = GetFilesOf(project.ProjectItems, test);
            ownFiles.Reverse();
            list.AddRange(ownFiles);

            if (includeReferenced) {
                List<Project> referencedProjects = project.GetReferencedProjects();
                foreach (Project referencedProj in referencedProjects) {
                    if (referencedProj.UniqueName != project.UniqueName) {
                        List<ProjectItem> l = GetFilesOf(referencedProj.ProjectItems, test);
                        l.Reverse();
                        list.AddRange(l);
                    }
                }
            }          

            return list;
        }

        private static List<ProjectItem> GetFilesOf(ProjectItems items,Predicate<ProjectItem> test) {
            List<ProjectItem> list = new List<ProjectItem>();

            if (items != null) {
                foreach (ProjectItem item in items) {
                    if (test==null || test(item)) {
                        list.Add(item);
                    } else {
                        if (item.ProjectItems != null && item.ProjectItems.Count > 0)
                            list.AddRange(GetFilesOf(item.ProjectItems, test));
                    }
                }
            }

            return list;
        }        

        public static List<Project> GetReferencedProjects(this Project project) {
            if (project == null)
                throw new ArgumentNullException("project");

            List<Project> list = new List<Project>();
                        
            VSProject proj = project.Object as VSProject;
            if (proj == null) {
                proj = project as VSProject;
                return list;
            }
            
            if (proj.References != null) {
                foreach (Reference r in proj.References)
                    if (r.SourceProject != null)
                        list.Add(r.SourceProject);
            }

            return list;
        }

        public static ProjectItem AddResourceDir(this Project project, string subdir) {
            string resourcesFolder;
            if (project.Kind.ToUpperInvariant() == WebSiteProject) {
                resourcesFolder = GlobalWebSiteResourcesFolder;
            } else {
                resourcesFolder = "Resources";
            }

            ProjectItem resItem = null;
            if (project.ProjectItems.ContainsItem(resourcesFolder)) {
                resItem = project.ProjectItems.Item(resourcesFolder);
            } else {
                resItem = project.ProjectItems.AddFolder(resourcesFolder, null);
            }

            ProjectItem subItem = null;
            if (resItem.ProjectItems.ContainsItem(subdir)) {
                subItem = resItem.ProjectItems.Item(subdir);
            } else {
                subItem = resItem.ProjectItems.AddFolder(subdir, null);
            }

            return subItem;
        }

        public static bool ContainsItem(this ProjectItems items, string item) {
            foreach (ProjectItem i in items)
                if (i.Name == item) return true;
            return false;
        }

        public static bool ContainsProjectItem(this Solution solution, ProjectItem item) {
            if (solution == null) return false;
            if (!solution.IsOpen) return false;
            if (item == null) return false;
            if (item.Object == null) return false;
            ProjectItem found = solution.FindProjectItem(item.GetFullPath());
            return found != null;
        }

        public static bool IsGenerated(this ProjectItem item) {
            if (item == null) return false;
            try {
                bool isCustomToolOutput = false;
                bool isDependant = false;
                bool isAspxCodeBehind = false;

                foreach (Property prop in item.Properties) {
                    if (prop.Name == "IsCustomToolOutput") {
                        isCustomToolOutput = (bool)prop.Value;
                    }
                    if (prop.Name == "IsDependentFile") {
                        isDependant = (bool)prop.Value;
                    }
                    if (prop.Name == "SubType") {
                        isAspxCodeBehind = (string)prop.Value == "ASPXCodeBehind";
                    }
                }
                return isCustomToolOutput || (isDependant && !isAspxCodeBehind);
            } catch (Exception) {
                return false;
            }
        }

        public static string GetFullPath(this ProjectItem item) {
            try {
                return (string)item.Properties.Item("FullPath").Value;
            } catch (Exception) {
                return null;
            }
        }

        public static string GetCustomTool(this ProjectItem item) {
            try {
                return (string)item.Properties.Item("CustomTool").Value;
            } catch (Exception) {
                return null;
            }
        }

        public static string GetCustomToolOutput(this ProjectItem item) {
            try {
                return (string)item.Properties.Item("CustomToolOutput").Value;
            } catch (Exception) {
                return null;
            }
        }

        public static string GetRelativeURL(this ProjectItem item) {
            try {
                return (string)item.Properties.Item("RelativeURL").Value;
            } catch (Exception) {
                return null;
            }
        }

        public static bool GetIsDependent(this ProjectItem item) {
            try {
                return (bool)item.Properties.Item("IsDependentFile").Value;
            } catch (Exception) {
                return false;
            }
        }
    }
}
