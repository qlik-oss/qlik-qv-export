using qlik_qv_export.QMSAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exception = System.Exception;

namespace qlik_qv_export
{
    class Program
    {
        private static Uri qms = new Uri("http" + "://localhost:4799/QMS/Service");
        private static string cloudDeploymentUrl = string.Empty;
        private static string mountOrRootFolder = string.Empty;
        private static IQMS2 client;
        private static Guid qvsId;
        private static string qvsCluster = string.Empty;
        private static string jwtToken = string.Empty;
        private static List<DocumentNode> userDocs;
        private static CommunicationSupport commSupport;
        private static QVSSettings qvsSettings;
        private static List<DocumentFolder> userDocFolders;
        
        static void Main(string[] args)
        {
            commSupport = new CommunicationSupport();
            client = CreateQMSAPIClient(qms);
            if (args.Length == 1 && args[0].Equals("-help"))
            {
                DisplayDocUploaderHelp();
            }
            
            
            if (args.Contains("-mode=link"))
            {
                RunInLinkMode(args);
            }
            else if(args.Contains("-mode=doc"))
            {
                RunInDocMode(args);
            }
            else
            {
                commSupport.PrintMessage("'mode' parameter missing or faulty", true);
            }
        }

        private static void RunInDocMode(string[] args)
        {
            try
            {
                foreach (var arg in args)
                {
                    string parameter = arg.Substring(0, arg.IndexOf('='));
                    string parameterValue = arg.Substring(arg.LastIndexOf('=') + 1);

                    switch (parameter.ToLower())
                    {
                        case "-cloudurl":
                            cloudDeploymentUrl = parameterValue;
                            break;
                        case "-documentfolder":
                            mountOrRootFolder = parameterValue;
                            break;
                        case "-qvscluster":
                            qvsCluster = parameterValue;
                            break;
                        case "-api_key":
                            jwtToken = parameterValue;
                            break;
                        default:
                            break;
                    }
                }
                if (string.IsNullOrEmpty(cloudDeploymentUrl) || string.IsNullOrEmpty(qvsCluster) || string.IsNullOrEmpty(jwtToken))
                {
                    commSupport.PrintMessage("Please supply required parameters", true);
                }
            }
            catch (Exception e)
            {
                commSupport.PrintMessage("Exception when reading parameters." + e.Message + " Run help command for information about usage", true);
            }
            try
            {
                SetUpStartValues();
                DistributeDocuments(commSupport);
            }
            catch(Exception e)
            {
                commSupport.PrintMessage("Exception: " + e.Message, true);
            }
        }

        private static void RunInLinkMode(string[] args)
        {
            new QlikQvExport(args, commSupport, client);
        }
 
        private static void SetUpStartValues()
        {
            client.ClearQVSCache(QVSCacheObjects.UserDocumentList);
            qvsId = FindServiceId(ServiceTypes.QlikViewServer, qvsCluster);
            qvsSettings = client.GetQVSSettings(qvsId, QVSSettingsScope.Folders);
            userDocFolders = client.GetUserDocumentFolders(qvsId, DocumentFolderScope.All);
            userDocs = GetUserDocuments();
        }

        private static Guid FindServiceId(ServiceTypes serviceType, string serviceName)
        {
            try
            {
                List<ServiceInfo> service = client.GetServices(serviceType);
                if (service != null && service.Count > 0)
                {
                    return service.FirstOrDefault(x => x.Name.Equals(serviceName)).ID;
                }
                else
                {
                    commSupport.PrintMessage("Could not find " + serviceName  + " service. Make sure cluster name is correct", true);
                }
            }
            catch (Exception)
            {
                commSupport.PrintMessage("Exception when trying to fetch " + serviceName + ". Make sure cluster name is correct", true);
            }
            return Guid.Empty;
        }

        private static void DisplayDocUploaderHelp()
        {
            string url = "https://" + "mycloud.somecloud.se/";
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("It can only be executed on the machine running the Qlikview Management service");
            Console.WriteLine("");
            Console.WriteLine("Usage when running in doc mode:");
            Console.WriteLine("-mode=<mode> -cloudUrl=<cloud API endpoint> -qvscluster=<qvs cluster name>  -API_key=<API Key> -documentFolder=<name of QVS folder>");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("-mode            [Required] Which mode should the program run in. 'doc' for uploading qlikview documents to cloud");
            Console.WriteLine("-cloudUrl        [Required] Url of cloud API endpoint, i.e target for document upload");
            Console.WriteLine("-qvscluster      [Required] Name of QlikView server cluster as displayed in QMC");
            Console.WriteLine("-API_key         [Required] The temporary API Key generated by cloud admin");
            Console.WriteLine("-documentFolder  [Optional] Name of QlikView server document folder, root or mount. All documents in folder will be uploaded. Leave empty for All Documents, type 'root' for Qvs root folder");
            Console.WriteLine("");
            Console.WriteLine("Example: qlik_qv_export.exe -mode=doc -cloudUrl=" + url + " -qvscluster=QVS@machineOne  -API_key=myTempApiKey -documentFolder=myMount");
            DisplayLinkExportHelp();
        }

        private static void DisplayLinkExportHelp()
        {
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Usage when running in link mode:");
            Console.WriteLine("-mode=<mode> -space=<name of space> -protocol=<http/https> -qvscluster=<qvs cluster name>  -qvwsmachine=<Qlikview webserver name> -category=<name of category as seen in accesspoint> -filename=<path to file containing exported links>");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("-mode        [Required] Which mode should the program run in. 'link' for exporting links to csv file.");
            Console.WriteLine("-filename    [Required] Name and path of csv file containing links");
            Console.WriteLine("-qvscluster  [Required] Name of Qvs cluster as displayed in QMC");
            Console.WriteLine("-qvwsmachine [Required] Name of Qvws as displayed in QMC");
            Console.WriteLine("-space       [Optional] The cloud space name");
            Console.WriteLine("-protocol    [Optional] The protocol used for connecting to Management Service. Can be http or https. Defaults to http");
            Console.WriteLine("-category    [Optional] Only add links belonging to specified category.");
            Console.WriteLine("");
            string path = @"C:\QvLinks\MyQvLinks.csv";
            Console.WriteLine("Example: qlik_qv_export.exe -mode=link -space=Common -protocol=http -qvscluster=QVS@machineOne  -qvwsmachine=QVWS@webServer -category=Sales -filename=" + path);
            commSupport.PrintMessage("", true);
        }

        private static string GetDocumentPath(DocumentNode userDoc)
        {
            
            string documentPath = string.Empty;

            DocumentFolder docFolder = userDocFolders.FirstOrDefault(x => x.ID.Equals(userDoc.FolderID));
            if (docFolder != null)
            {
                QVSMount temp = qvsSettings.Folders.UserDocumentMounts.FirstOrDefault(x => x.Name.Equals(docFolder.General.Path));
                string relativePath = string.IsNullOrEmpty(userDoc.RelativePath) ? "" : userDoc.RelativePath + "\\";
                if (temp != null)
                {
                    documentPath = temp.Path + "\\" + relativePath;
                }
                else
                {
                    documentPath = qvsSettings.Folders.UserDocumentRootFolder + "\\" + relativePath;
                }
            }
            return documentPath;
        }

        private static void DistributeDocuments(CommunicationSupport commSupport)
        {
            commSupport.PrintMessage(userDocs.Count + " document(s) marked for upload", false);
            foreach (DocumentNode doc in userDocs)
            {
                string fullDocumentPath = GetDocumentPath(doc) + doc.Name;
                _ = commSupport.DistributeDocument(fullDocumentPath, cloudDeploymentUrl, doc.ID.ToString(), jwtToken).GetAwaiter().GetResult();
            }
        }

        private static Guid FindFolderId()
        {
            DocumentFolder docFolder = null;
            if (mountOrRootFolder.Equals("root"))
            {
                docFolder = userDocFolders.Find(x => string.IsNullOrEmpty(x.General.Path));
            }
            else
            {
                docFolder = userDocFolders.Find(x => x.General.Path.Equals(mountOrRootFolder));
            }

            if (docFolder != null)
            {
                return docFolder.ID;
            }

            return Guid.Empty;
        }

        private static List<DocumentNode> GetUserDocuments()
        {
            List<DocumentNode> userDocuments = client.GetUserDocuments(qvsId);//Need to fetch all for full recursive look up
            if (string.IsNullOrEmpty(mountOrRootFolder))
            {
                return userDocuments;
            }
            else
            {
                Guid folderId = FindFolderId();
                List<DocumentNode> retValue = new List<DocumentNode>();
                foreach (DocumentNode node in userDocuments)
                {
                    if(node.FolderID == folderId)
                    {
                        retValue.Add(node);
                    }
                }
                return retValue;
            }
        }

        private static IQMS2 CreateQMSAPIClient(Uri uri)
        {
            QMS2Client client = null;

            try
            {
                client = new QMS2Client("BasicHttpBinding_IQMS2", uri.AbsoluteUri);
                ServiceKeyEndpointBehavior serviceKeyEndpointBehavior = new ServiceKeyEndpointBehavior();
                client.Endpoint.Behaviors.Add(serviceKeyEndpointBehavior);
                ServiceKeyClientMessageInspector.ServiceKey = client.GetTimeLimitedServiceKey();
            }
            catch (Exception e)
            {
                commSupport.PrintMessage("Exception when creating QMs client, run help command for information about usage. Exeception: " + e.Message, true);
            }
            return client;
        }
    }
}
