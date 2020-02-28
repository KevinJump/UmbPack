using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Semver;

using Umbraco.Packager.CI.Properties;

namespace Umbraco.Packager.CI.Verbs
{

    /// <summary>
    ///  Command line options for the Init verb
    /// </summary>
    [Verb("init", HelpText = "HelpInit", ResourceType = typeof(HelpTextResource))]
    public class InitOptions
    {
        [Value(0,
            MetaName = "Folder",
            HelpText = "HelpInitFolder", ResourceType = typeof(HelpTextResource))]
        public string Folder { get; set; }

        /* (not supportd yet)  
        [Option("nuspec", 
            HelpText = "HelpTextNuspec",
            ResourceType = typeof(HelpTextResource))]
        public string NuSpecFile { get; set; }
        */
    }


    /// <summary>
    ///  Init command, asks the user some questions makes a package.xml file
    /// </summary>
    /// <remarks>
    ///  Works like npm init, makes some guesses as to what defaults to use
    ///  and lets the user enter values, at the end it writes out a package.xml file
    /// </remarks>
    internal static class InitCommand
    {
        public static void RunAndReturn(InitOptions options)
        {
            var path = string.IsNullOrWhiteSpace(options.Folder) ? "." : options.Folder;

            var currentFolder = new DirectoryInfo(path);

            var packageFile = GetPackageFile(options.Folder);

            var setup = new PackageSetup();

            Console.WriteLine(Resources.Init_Header);
            Console.WriteLine();

            // gather all the user input

            setup.Name = GetUserInput(Resources.Init_PackageName, Path.GetFileName(currentFolder.Name));

            setup.Description = GetUserInput(Resources.Init_Description, Defaults.Init_Description);

            setup.Version = GetVersionString(Resources.Init_Version, Defaults.Init_Version);

            setup.Url = GetUserInput(Resources.Init_Url, Defaults.Init_Url);

            setup.UmbracoVersion = GetVersionString(Resources.Init_UmbracoVersion, Defaults.Init_UmbracoVersion);

            setup.Author = GetUserInput(Resources.Init_Author, Environment.UserName);

            setup.Website = GetUserInput(Resources.Init_Website, setup.Url);

            (setup.Licence, setup.LicenceUrl) = GetLicence(Resources.Init_Licence, Defaults.Init_Licence);

            // play it back for confirmation
            Console.WriteLine();
            Console.WriteLine(Resources.Init_Confirm, packageFile);

            var node = MakePackageFile(setup);

            Console.WriteLine(node.Element("info").ToString());

            // confirm 

            var confirm = GetUserInput(Resources.Init_Prompt, Defaults.Init_Prompt).ToUpper();
            if (confirm[0] == 'Y')
            {
                // save xml to disk.

                node.Save(packageFile);
                Console.WriteLine(Resources.Init_Complete);
                Environment.Exit(0);
            }
            else
            {
                Environment.Exit(1);
            }
        }

        /// <summary>
        ///  Make a package xml from the options
        /// </summary>
        /// <param name="options">options entered by the user</param>
        /// <returns>XElement containing the package.xml info</returns>
        private static XElement MakePackageFile(PackageSetup options)
        {
            var node = new XElement("umbPackage");

            node.Add(new XElement("files"));

            var info = new XElement("info");

            var package = new XElement("package");
            package.Add(new XElement("name", options.Name));
            package.Add(new XElement("version", options.Version));
            package.Add(new XElement("iconUrl", ""));
            package.Add(new XElement("licence", options.Licence,
                new XAttribute("url", options.LicenceUrl)));
            
            package.Add(new XElement("url", options.Url));
            package.Add(new XElement("requirements",
                new XAttribute("type", "strict"),
                new XElement("major", options.UmbracoVersion.Major),
                new XElement("minor", options.UmbracoVersion.Minor),
                new XElement("patch", options.UmbracoVersion.Patch)));
            info.Add(package);

            info.Add(new XElement("author",
                        new XElement("name", options.Author),
                        new XElement("website", options.Website)));

            info.Add(new XElement("contributors"));

            info.Add(new XElement("readme",
                new XCData(options.Description)));

            node.Add(info);

            node.Add(new XElement("DocumentTypes"));
            node.Add(new XElement("Templates"));
            node.Add(new XElement("Stylesheets"));
            node.Add(new XElement("Macros"));
            node.Add(new XElement("DictionaryItems"));
            node.Add(new XElement("Languages"));
            node.Add(new XElement("DataTypes"));
            node.Add(new XElement("Actions"));

            return node;

        }

        /// <summary>
        ///  Prompt the user to enter a licence (or use default)
        /// </summary>
        private static (string, string) GetLicence(string prompt, string defaultValue)
        {
            while (true)
            {
                var licenceValue = GetUserInput(prompt, defaultValue);
                var url = GetLicenceUrl(licenceValue);

                if (url != null)
                {
                    return (licenceValue, url);
                }
                else
                {
                    Console.WriteLine(Resources.Init_InvalidLicence);
                }
            }
        }

        /// <summary>
        ///  Workout the URL for the licence based on the string value
        /// </summary>
        /// <param name="licenceName">Licence Name (e.g MIT)</param>
        /// <returns>URL for the licence file</returns>
        private static string GetLicenceUrl(string licenceName)
        {
            if (licenceName.Equals("UNLICENSED", StringComparison.InvariantCultureIgnoreCase)) return "";

            // get the list of licences from github, see if we can match this to one of them.
            using (var client = new HttpClient())
            {
                try
                {
                    client.BaseAddress = new Uri("https://api.github.com/");
                    var response = Task.Run(() => client.GetAsync("/licenses")).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var content = Task.Run(() => response.Content.ReadAsStringAsync()).Result;
                        var licences = JsonConvert.DeserializeObject<JArray>(content);

                        var licence = licences
                            .FirstOrDefault(x => x.Value<string>("spdx_id")
                            .Equals(licenceName, StringComparison.InvariantCultureIgnoreCase));

                        if (licence != null)
                        {
                            return licence.Value<string>("url");
                        }
                        else
                        {
                            return null;
                            // Console.WriteLine(Resources.Init_InvalidLicence);
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Unable to retreive a list of licences");
                }
            }

            // else - if the API fails or we can't find the licence in the list. 
            // then at least let MIT through.
            if (licenceName.Equals("MIT", StringComparison.InvariantCultureIgnoreCase))
            {
                return "https://opensource.org/licenses/MIT";
            }

            return string.Empty;
        }
        
        /// <summary>
        ///  Prompts the user for version string and validates it.
        /// </summary>
        /// <param name="prompt">text to put in prompt</param>
        /// <param name="defaultValue">default value if user just presses enter</param>
        /// <returns>SemVersion compatible version</returns>
        private static SemVersion GetVersionString(string prompt, string defaultValue)
        {
            while (true)
            {
                var versionString = GetUserInput(prompt, defaultValue);
                if (SemVersion.TryParse(versionString, out SemVersion version))
                {
                    return version;
                }
                else
                {
                    Console.WriteLine(Resources.Init_InvalidVersion, versionString);
                }
            }
        }

        /// <summary>
        ///  Prompt the user for some input, return a default value if they just press enter
        /// </summary>
        /// <param name="prompt">Prompt for user</param>
        /// <param name="defaultValue">Default value if they just press enter</param>
        /// <returns>user value or default</returns>
        private static string GetUserInput(string prompt, string defaultValue)
        {
            Console.Write($"{prompt}: ");
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                Console.Write($"({defaultValue}) ");
            }

            var value = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return value;
        }

        /// <summary>
        ///  Validates the path to where the package file is going to be created
        /// </summary>
        /// <param name="packageFile">path to a package file</param>
        private static string GetPackageFile(string packageFile)
        {
            var filePath = Path.Combine(".", "package.xml");

            if (!string.IsNullOrWhiteSpace(packageFile))
            {
                if (Path.HasExtension(packageFile))
                {
                    filePath = packageFile;
                }
                else
                {
                    filePath = Path.Combine(packageFile, "package.xml");
                }
            }

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Console.WriteLine(Resources.Init_MissingFolder, Path.GetDirectoryName(filePath));
                Environment.Exit(2);
            }

            return filePath;

        }

        /// <summary>
        ///  Package Setup options
        /// </summary>
        /// <remarks>
        ///  Options that are used in building the package.xml file
        /// </remarks>
        private class PackageSetup
        {
            public string Name { get; set; }
            public SemVersion Version { get; set; }
            public string Url { get; set; }
            public string Author { get; set; }
            public string Website { get; set; }
            public string Licence { get; set; }
            public string LicenceUrl { get; set; }

            public SemVersion UmbracoVersion { get; set; }
            public string Description { get; set; }
        }
    }
}
