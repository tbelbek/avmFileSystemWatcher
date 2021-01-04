using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace avmFileSystemWatcher
{
    /// <summary>
    /// Study Case
    /// http://www.linhadecodigo.com.br/artigo/3097/monitorando-arquivos-e-diretorios-com-filesystemwatcher.aspx
    /// </summary>
    public class avmFileSystemWatcherProcess : IDisposable
    {
        private string Out = string.Empty;
        private const int _timeoutDefault = 30000;

        public string Log { get { return Out; } }

        FileSystemWatcher fsw = null;
        string sourcePath = string.Empty;
        string sourceWildcard = string.Empty;
        string destinationPath = string.Empty;
        int timeout = _timeoutDefault;

        // thinking about this... to use with unique event of class... maybe future...
        public bool running = false;

        public bool _stopCommand = false;

        public avmFileSystemWatcherProcess()
        {
            loadConfig();
        }

        public avmFileSystemWatcherProcess(string source = "", string destination = "", int _timeout = _timeoutDefault)
        {
            loadConfig(source, destination);

            if (timeout != _timeoutDefault)
            {
                this.timeout = _timeout;
            }
        }

        public bool Init()
        {
            try
            {
                if (this.fsw == null)
                {
                    this.fsw = new FileSystemWatcher();
                    this.fsw.Changed += new FileSystemEventHandler(fsw_Changed);
                    this.fsw.Created += new FileSystemEventHandler(fsw_Created);
                    //this.fsw.Deleted += new FileSystemEventHandler(fsw_Deleted);
                    this.fsw.Renamed += new RenamedEventHandler(fsw_Renamed);
                }

                Load();

                return true;
            }
            catch //(Exception e)
            {
                return false;
            }
        }

        ~avmFileSystemWatcherProcess()
        {
            if (fsw != null)
            {
                fsw.Dispose();
            }
            fsw = null;
        }

        private void loadConfig(string source = "", string destination = "", string wildcard = "*.*")
        {
            if (!string.IsNullOrEmpty(source) &&
                !string.IsNullOrEmpty(destination))
            {
                this.sourcePath = source;
                this.sourceWildcard = wildcard;
                this.destinationPath = destination;
            }
            else
            {
                this.sourcePath = ConfigurationManager.AppSettings["SOURCE"];
                this.sourceWildcard = ConfigurationManager.AppSettings["SOURCE-WILDCARD"];
                this.destinationPath = ConfigurationManager.AppSettings["DESTINATION"];
                try
                {
                    this.timeout = Convert.ToInt32(ConfigurationManager.AppSettings["TIMEOUT"]);
                }
                catch
                {
                    this.timeout = _timeoutDefault;
                }
            }
        }
        private void Load()
        {
            try
            {
                // SOURCE PATH MUST EXIST
                if (!Directory.Exists(sourcePath))
                {
                    try
                    {
                        Directory.CreateDirectory(sourcePath);
                    }
                    catch
                    {
                        throw new Exception("SOURCE PATH IS INVALID.");
                    }
                    if (!Directory.Exists(destinationPath))
                    {
                        throw new Exception("SOURCE PATH IS INVALID.");
                    }
                }

                // SOURCE PATH
                this.fsw.Path = sourcePath;

                // TYPES
                this.fsw.Filter = this.sourceWildcard;

                // EVENTS TO NOTIFY
                this.fsw.NotifyFilter = /*NotifyFilters.Attributes |*/
                                        NotifyFilters.CreationTime |
                                        //NotifyFilters.DirectoryName |
                                        NotifyFilters.FileName |
                                        //NotifyFilters.LastAccess |
                                        NotifyFilters.LastWrite |
                                        //NotifyFilters.Security |
                                        NotifyFilters.Size;

                // THIS PARAMETER, TURNS MONITOR ON
                this.fsw.EnableRaisingEvents = true;

                // INCLUDE SUB-FOLDERS ?
                this.fsw.IncludeSubdirectories = true;

                running = true;

                // WAIT TIMEOUT
                var wcr = fsw.WaitForChanged(WatcherChangeTypes.All, this.timeout);

                Out = string.Empty;

                // TIMEOUT
                if (wcr.TimedOut)
                {
                    Out += "WAITING FOR 30 SECONDS..." + Environment.NewLine;
                    Console.WriteLine("WAITING FOR 30 SECONDS");
                    validateForceStop();
                }
                else
                {
                    var eventMessage = $"EVENT: NAME {wcr.Name} TYPE {wcr.ChangeType.ToString()}";
                    Out += eventMessage + Environment.NewLine;
                    Console.WriteLine(eventMessage);
                }
            }
            catch (Exception ex)
            {
                running = false;
                throw ex;
            }
        }

        private void fsw_Changed(object sender, FileSystemEventArgs e)
        {
            if (!CheckIsFile(e)) return;

            var msg = $"UPDATED: FILE {e.FullPath} | NAME {e.Name} | EVENT {e.ChangeType.ToString()}";

            File.Copy(Path.Combine(e.FullPath), Path.Combine(this.destinationPath, Path.GetFileName(e.FullPath)), true);

            Console.WriteLine(msg);

            this.Out += msg + Environment.NewLine;

            this.validateForceStop();

        }

        private static bool CheckIsFile(FileSystemEventArgs e)
        {
            var attr = File.GetAttributes(e.FullPath);

            //detect whether its a directory or file
            return !attr.HasFlag(FileAttributes.Directory);
        }

        private void fsw_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (!CheckIsFile(e)) return;

                if (!Directory.Exists(Path.GetDirectoryName(e.FullPath)))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(e.FullPath));
                    }
                    catch
                    {
                        throw new Exception("SOURCE PATH IS INVALID.");
                    }
                    if (!Directory.Exists(Path.GetDirectoryName(e.FullPath)))
                    {
                        throw new Exception("SOURCE PATH IS INVALID.");
                    }
                }

                if (!Directory.Exists(destinationPath))
                {
                    try
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    catch
                    {
                        throw new Exception("DESTINATION PATH IS INVALID.");
                    }
                    if (!Directory.Exists(destinationPath))
                    {
                        throw new Exception("SOURCE PATH IS INVALID.");
                    }
                }

                File.Copy(Path.Combine(e.FullPath), Path.Combine(destinationPath, Path.GetFileName(e.FullPath)), true);

                var msg1 = $"CREATED: {e.FullPath} | {e.Name} | {e.ChangeType.ToString()}";
                Out += msg1 + Environment.NewLine;

                var msg2 = $"COPIED: {e.FullPath} | {this.destinationPath + e.Name}";
                Out += msg1 + Environment.NewLine;

                Console.WriteLine(msg1);
                Console.WriteLine(msg2);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine(ex?.InnerException?.Message);
            }
            validateForceStop();
        }

        private void fsw_Deleted(object sender, FileSystemEventArgs e)
        {
            var msg = $"DELETED: {e.FullPath} | {e.Name} | {e.ChangeType.ToString()}";

            Console.WriteLine(msg);

            Out += msg + Environment.NewLine;

            validateForceStop();
        }

        private void fsw_Renamed(object sender, RenamedEventArgs e)
        {

            if (!CheckIsFile(e)) return;

            var msg = $"NAME UPDATED: {e.FullPath} | {e.Name} | {e.ChangeType.ToString()}";

            File.Copy(Path.Combine(e.FullPath), Path.Combine(destinationPath, Path.GetFileName(e.FullPath)), true);

            Console.WriteLine(msg);

            Out += msg;

            validateForceStop();
        }

        private void validateForceStop()
        {
            if (this._stopCommand)
            {
                _stopCommand = false;
                Environment.Exit(2);
            }
        }

        public void Dispose()
        {
            fsw.Dispose();
            GC.WaitForPendingFinalizers();
            GC.SuppressFinalize(this);
        }
    }
}
