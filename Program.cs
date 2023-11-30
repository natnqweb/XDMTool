using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
// Why XDMTool XDMTool stand for External Dependecy Manager
namespace XDMTool
{

    public class FileDownloader
    {
        private readonly HttpClient httpClient;

        public FileDownloader()
        {
            httpClient = new HttpClient();
        }

        public async Task DownloadFileAsync(string url, string destinationPath)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        using (FileStream fileStream = File.Create(destinationPath))
                        {
                            await contentStream.CopyToAsync(fileStream);
                        }
                    }
                    Console.WriteLine("File downloaded successfully to: " + destinationPath);
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }

    public class RefPath
    {
        Workspace parentWorkspace;

        public string PathOnDisk;
        string _fullPathOnServer = string.Empty;
        public RefPath(Workspace workspace, string PathOnDisk) 
        {
            parentWorkspace = workspace;
            this.PathOnDisk = PathOnDisk;
        }

        public string GetCurrentFileDir()
        {
            return Path.GetDirectoryName(PathOnDisk);
        }

        public string GetFullPathOnServer()
        {
            if (!string.IsNullOrEmpty(_fullPathOnServer))
                return _fullPathOnServer;

            try
            {
                using (StreamReader sr = new StreamReader(PathOnDisk))
                {
                    string line;
                    // Read and display lines from the file until the end is reached
                    while ((line = sr.ReadLine()) != null)
                    {
                        if(!string.IsNullOrEmpty(line))
                        {
                            _fullPathOnServer = parentWorkspace?.GetServer();
                            _fullPathOnServer += "/";
                            _fullPathOnServer += line.Trim();
                            break;
                        }
                    }
                }

            }
            catch
            {
                _fullPathOnServer = string.Empty;
            }

            return _fullPathOnServer;
           
        }
    }

    public class Workspace
    {
        string _name;
        string _path;
        string _server;
        public string ConfigPath = string.Empty;
        public Workspace(string strName, string path, string server)
        {
            _name = strName;
            _path = path;
            _server = server;
        }

        public string GetName()
        {
            return _name;
        }
        public string GetPath()
        {
            if (_path == "./" || string.IsNullOrWhiteSpace(_path) || _path == ".\\")
                return string.Empty;
            return _path;
        }

        public string GetServer()
        {
            return _server;
        }

        static void ParseDirectoryRefFiles(string directory, ref List<string> files)
        {
            try
            {
                foreach (string file in Directory.GetFiles(directory))
                {
                    if(file.Contains(Constants.XDM_REF_PATH))
                    {
                        files.Add(file);
                        break;
                    }
                }

                foreach (string subDirectory in Directory.GetDirectories(directory))
                {
                    ParseDirectoryRefFiles(subDirectory, ref files);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error accessing directory: " + e.Message);
            }
        }

        public string GetWorkspaceDirectory()
        {
            string workspaceDir = string.Empty;
            try
            {
                workspaceDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ConfigPath), GetPath()));
                workspaceDir += "\\";
                workspaceDir += GetName();
            }
            catch
            {}
            return workspaceDir;
        }
        public List<RefPath> GetAllRefPathsInWorkspace()
        {
            List<RefPath> refPaths = new List<RefPath>();
            string workspaceDir = GetWorkspaceDirectory();
            if (string.IsNullOrEmpty(workspaceDir))
                return refPaths;

            List<string> pathFiles = new List<string>();
            ParseDirectoryRefFiles(workspaceDir, ref pathFiles);
            foreach (string file in pathFiles)
            {
               refPaths.Add(new RefPath(this, file));
            }


            return refPaths;
        }
    }
    public class Command
    {
        public string Name;
        public string Param;
        public bool MatchesCommandName(string[] possibleCommands)
        {
            foreach (var command in possibleCommands)
            {
                try
                {
                    if (command.Equals(Name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (Exception)
                {}
                
            }
            return false;
        }
    }
    static public class Constants
    {
        public static readonly string[] COMMANDS_CONFIGURATION_FILE_PATH = { "-Config","-cnfg" ,"-configurationPath" };
        public static readonly string XML_WORKSPACE = "Workspace";
        public static readonly string XML_WORKSPACE_NAME_ATTR = "Name";
        public static readonly string XML_WORKSPACE_SERVER = "Server";
        public static readonly string XML_WORKSPACE_PATH = "Path";
        public static readonly string XDM_REF_PATH = "xdm_ref_path.txt";
    }
 
    internal class Program
    {
        
        public static List<Command> GetCommandLine(string[] args)
        {
            var commandList = new List<Command>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    var command = new Command { Name = args[i] };

                    // Check if there's a parameter available after the command
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        command.Param = args[i + 1];
                        i++; // Move to the next argument (parameter)
                    }

                    commandList.Add(command);
                }
            }

            return commandList;
        }
        static public bool GetNodeAttribute(XmlNode node, string name, out string value)
        {
            foreach (XmlAttribute attribute in node.Attributes)
            {
                if(attribute.Name == name)
                {
                    value = attribute.Value;
                    return true;
                }

            }
            value = string.Empty;
            return false;
        }
        static public bool GetNodeInnerText(XmlNode workspaceNode, string name, out string value)
        {
            try
            {
                var node = workspaceNode?.SelectSingleNode("./" + name);

                if (node != null)
                {
                    if (node.Name == name)
                    {
                        value = node.InnerText;
                        return true;
                    }
                }
            }
            catch { }

            value = string.Empty;
            return false;
        }
        static public bool ParseXml(string xmlPath, ref List<Workspace> workspaces)
        {
            if (string.IsNullOrEmpty(xmlPath))
                return false;

            XmlDocument xmlDocument = new XmlDocument();

            try
            {
                xmlDocument.Load(xmlPath);
                foreach (XmlNode workspaceNode in xmlDocument.GetElementsByTagName(Constants.XML_WORKSPACE)) 
                {
                    if (!GetNodeAttribute(workspaceNode, Constants.XML_WORKSPACE_NAME_ATTR, out var workspaceName) ||
                        !GetNodeInnerText(workspaceNode, Constants.XML_WORKSPACE_SERVER, out var server)           ||
                        !GetNodeInnerText(workspaceNode, Constants.XML_WORKSPACE_PATH, out var path)               ||
                        string.IsNullOrEmpty(path)                                                                 ||
                        string.IsNullOrEmpty(workspaceName)                                                        ||
                        string.IsNullOrEmpty(server))
                    {
                        continue;
                    }

                    workspaces.Add(new Workspace(workspaceName, path, server) { ConfigPath = xmlPath });
                }
            }
            catch
            {
                return false;
            }
           

            return workspaces.Count > 0;
        }
        static void OnReturn()
        {
            Console.ReadKey();
        }
        static void Main(string[] args)
        {
            try
            {
                string _pathToConfiguration = string.Empty;
                var commandList = GetCommandLine(args);
                if (commandList.Count == 0)
                {
                    Console.WriteLine("ERROR: no command line args provide -config <path>");
                    OnReturn();
                    return ;
                }


                foreach (var command in commandList)
                {
                    if (command.MatchesCommandName(Constants.COMMANDS_CONFIGURATION_FILE_PATH) && !string.IsNullOrEmpty(command.Param))
                    {
                        _pathToConfiguration = command.Param;
                    }
                }


                if (!(!string.IsNullOrEmpty(_pathToConfiguration) && File.Exists(_pathToConfiguration)))
                {
                    Console.WriteLine("ERROR: provided configuration file doesn't exists.");
                    OnReturn();
                    return;
                }

                List<Workspace> _workspaces = new List<Workspace>();
                if (!ParseXml(_pathToConfiguration, ref _workspaces))
                {
                    Console.WriteLine("ERROR: provided configuration file is not correct");
                    OnReturn();
                    return;
                }
                List<Task> tasks = new List<Task>();
                FileDownloader fileDownloader = new FileDownloader();
                foreach (var workspace in _workspaces)
                {
                    foreach (var refPath in workspace.GetAllRefPathsInWorkspace())
                    {
                        Console.WriteLine(refPath.GetFullPathOnServer());
                        tasks.Add(fileDownloader.DownloadFileAsync(refPath.GetFullPathOnServer(), refPath.GetCurrentFileDir() + "/file.pdf"));
                    }
                }

                foreach (var task in tasks)
                {
                    task.Wait();
                }
              
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);

            }
            OnReturn();

        }
    }
}
