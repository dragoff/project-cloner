using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace ProjectCloner
{
    [Serializable]
    public class Project : ICloneable
    {
        public string Name;
        public string ProjectPath;
        public string RootPath;
        public string AssetPath;
        public string ProjectSettingsPath;
        public string ProjectSettingsAssetPath;
        public string LibraryPath;
        public string PackagesPath;
        public string AutoBuildPath;
        public string LocalPackages;
        public string CompanyName;
        public string ProductName;
        public bool IsLinkAssetFolder;
        public bool IsLinkProjectSettingsFolder;

        private char[] separator = new char[1] { '/' };
        
        public Project() { }

        /// <summary>
        /// Initialize the project object by parsing its full path returned by Unity into a bunch of individual folder names and paths.
        /// </summary>
        /// <param name="path"></param>
        public Project(string path)
        {
            CompanyName = PlayerSettings.companyName;
            ProductName = PlayerSettings.productName;
            ParsePath(path);
        }

        public Project(string path, bool isLinkAssetFolder, bool isLinkProjectSettingsFolder)
        {
            IsLinkAssetFolder = isLinkAssetFolder;
            IsLinkProjectSettingsFolder = isLinkProjectSettingsFolder;
            CompanyName = PlayerSettings.companyName;
            ProductName = PlayerSettings.productName;
            ParsePath(path);
        }

        /// <summary>
        /// Create a new object with the same settings
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            Project newProject = new Project
            {
                RootPath = RootPath,
                ProjectPath = ProjectPath,
                AssetPath = AssetPath,
                ProjectSettingsPath = ProjectSettingsPath,
                LibraryPath = LibraryPath,
                Name = Name,
                separator = separator,
                PackagesPath = PackagesPath,
                AutoBuildPath = AutoBuildPath,
                LocalPackages = LocalPackages,
            };

            return newProject;
        }


        /// <summary>
        /// Update the project object by renaming and reparsing it. Pass in the new name of a project, and it'll update the other member variables to match.
        /// </summary>
        /// <param name="newName"></param>
        public void UpdateNewName(string newName)
        {
            Name = newName;
            ParsePath(RootPath + "/" + Name + "/Assets");
        }

        /// <summary>
        /// Debug override so we can quickly print out the project info.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string printString = Name + "\n" +
                                 RootPath + "\n" +
                                 ProjectPath + "\n" +
                                 AssetPath + "\n" +
                                 ProjectSettingsPath + "\n" +
                                 PackagesPath + "\n" +
                                 AutoBuildPath + "\n" +
                                 LocalPackages + "\n" +
                                 LibraryPath;
            return (printString);
        }

        private void ParsePath(string path)
        {
            //Unity's Application functions return the Assets path in the Editor. 
            ProjectPath = path;

            //pop off the last part of the path for the project name, keep the rest for the root path
            List<string> pathArray = ProjectPath.Split(separator).ToList<string>();
            Name = pathArray.Last();

            pathArray.RemoveAt(pathArray.Count() - 1);
            RootPath = string.Join(separator[0].ToString(), pathArray.ToArray());

            AssetPath = ProjectPath + "/Assets";
            ProjectSettingsPath = ProjectPath + "/ProjectSettings";
            ProjectSettingsAssetPath = ProjectPath + "/ProjectSettings/ProjectSettings.asset";
            LibraryPath = ProjectPath + "/Library";
            PackagesPath = ProjectPath + "/Packages";
            AutoBuildPath = ProjectPath + "/AutoBuild";
            LocalPackages = ProjectPath + "/LocalPackages";
        }

        public void UpdateProductCompanyNames()
        {
            if (IsLinkProjectSettingsFolder)
                return;

            using (var input = File.OpenText(ProjectSettingsAssetPath))
            {
                string line;
                while (null != (line = input.ReadLine()))
                {
                    if (line.Contains("companyName: "))
                        CompanyName = line.Replace("  companyName: ", "");
                    if (line.Contains("productName: "))
                        ProductName = line.Replace("  productName: ", "");
                }
            }
        }

        public void SetProductCompanyNames(string company, string product)
        {
            if (IsLinkProjectSettingsFolder)
                return;

            string[] arr = File.ReadAllLines(ProjectSettingsAssetPath);
            using (var writer = new StreamWriter(ProjectSettingsAssetPath))
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    string line = arr[i];
                    if (line.Contains("companyName:"))
                        line = $"  companyName: {company}";
                    if (line.Contains("productName:"))
                        line = $"  productName: {product}";
                    writer.WriteLine(line);
                }
            }
            CompanyName = company;
            ProductName = product;
        }
    }
}
