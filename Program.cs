namespace Tuplinator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    internal class Program
    {
        private static Regex ClassRegex
        {
            get { return new Regex("(public|private) ? (static|partial|abstract)? ?class ?([a-zA-Z0-9]*)"); }
        }

        private static Regex PropertyRegex
        {
            get { return new Regex("(public|private) ?(virtual)? ?([a-zA-Z0-9]*) ?([a-zA-Z0-9]*) ?{ get; set; }"); }
        }

        private static Regex GenericRegex
        {
            get { return new Regex("(public|private) ?(virtual)? ? ([a-zA-Z0-9]*[<]([a-zA-Z0-9,]*)[>]) ?[a-zA-Z0-9]* ?{ get; set; }"); }
        }

        private static void Main(string[] args)
        {
            if (args != null && !string.IsNullOrWhiteSpace(args[0]))
            {
                var folder = Path.GetFullPath(args[0]);

                var projects = GetProjectFilePaths(Directory.GetFiles(folder, "*.sln")[0]);
                var csProjFilePaths = projects.Select(o => folder + "\\" + o);
                var csFilePaths = new List<string>();
                foreach (var csProjFilePath in csProjFilePaths)
                {
                    csFilePaths.AddRange(GetCsFilePaths(csProjFilePath));
                }

                var csFiles = new List<string>();
                foreach (var csFilePath in csFilePaths)
                {
                    csFiles.Add(File.ReadAllText(csFilePath));
                }

                var classMetadata = new List<ClassMetadata>();
                foreach (var csFile in csFiles.Where(o => ClassRegex.IsMatch(o) && PropertyRegex.IsMatch(o)))
                {
                    classMetadata.Add(GetClassMetadata(csFile));
                }

                foreach (var metadata in classMetadata)
                {
                    for (var i = 0; i < csFiles.Count; i++)
                    {
                        csFiles[i] = PropertyRegex.Replace(csFiles[i], o =>
                        {
                            var propertyClassName = o.Groups[3].Value;
                            var property = o.Value;
                            if (propertyClassName == metadata.Name)
                            {
                                property = new Regex(metadata.Name).Replace(o.Value, metadata.Tuple, 1);
                            }

                            return property;
                        });

                        csFiles[i] = GenericRegex.Replace(csFiles[i], o =>
                        {
                            var propertyClassNames = o.Groups[4].Value.Split(',');
                            var property = o.Value;
                            if (propertyClassNames.Contains(metadata.Name))
                            {
                                property = new Regex(metadata.Name).Replace(o.Value, metadata.Tuple, 1);
                            }

                            return property;
                        });
                    }
                }

            }
            else
            {
                throw new ArgumentNullException(null, "Pass path to project folder as first arguiment");
            }
        }

        public static List<string> GetProjectFilePaths(string sln)
        {
            var lines = File.ReadAllLines(sln);
            var regex = new Regex("^Project\\(\"\\{(.*) = \"([A-Za-z0-9.]*)\", \"((.*)\\.csproj)\", (.*)$");
            var unfilteredprojectPaths = lines.Where(o => regex.IsMatch(o));
            var filteredprojectPaths = unfilteredprojectPaths.Select(o => regex.Matches(o)[0].Groups[3].Value);
            return filteredprojectPaths.ToList();
        }

        public static List<string> GetCsFilePaths(string csProj)
        {
            var lines = File.ReadAllLines(csProj);
            var regex = new Regex(@"<Compile Include=""(([A-Za-z0-9 -_\\]*).cs)\""(.*)>$");
            var unfilteredCsPaths = lines.Where(o => regex.IsMatch(o));
            var filteredCsPaths = unfilteredCsPaths.Select(o => regex.Matches(o)[0].Groups[1].Value);
            return filteredCsPaths.Select(o => csProj.Substring(0, csProj.LastIndexOf('\\')) + "\\" + o).ToList();
        }

        public static ClassMetadata GetClassMetadata(string code)
        {
            var classMetadata = new ClassMetadata {Name = ClassRegex.Match(code).Groups[3].Value};
            classMetadata.PropertiesAndTypes.AddRange(GetProperties(code));

            return classMetadata;
        }

        public static List<Tuple<string, string>> GetProperties(string code)
        {
            var properties = PropertyRegex.Matches(code).Cast<Match>().Select(o => new Tuple<string, string>(o.Groups[3].Value, o.Groups[4].Value)).ToList();
            return properties;
        }
    }

    public class ClassMetadata
    {
        public ClassMetadata()
        {
            PropertiesAndTypes = new List<Tuple<string, string>>();
        }

        public List<Tuple<string, string>> PropertiesAndTypes { get; set; }

        public string Name { get; set; }

        public string Tuple
        {
            get { return string.Format("Tuple<{0}>", string.Join(",", PropertiesAndTypes.Select(o => o.Item1))); }
        }
    }
}
