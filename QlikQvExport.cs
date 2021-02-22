using qlik_qv_export.QMSAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exception = System.Exception;

namespace qlik_qv_export
{
    class QlikQvExport
    {
        private List<string> links;
        private string delimiter = ";";
        private CommunicationSupport commSupport;
        private IQMS client;

        public QlikQvExport(CommunicationSupport commSup, IQMS qmsClient)
        {
            client = qmsClient;
            commSupport = commSup;
        }

        public void ExportLinks(string space, string protocol, string qvsCluster, string qvwsMachine, string category, string csvFileName)
        {
            protocol = string.IsNullOrEmpty(protocol) ? "http" : protocol;
            links = new List<string>();
            try
            {
                if (client != null)
                {
                    ServiceInfo qvs = FindService(client, ServiceTypes.QlikViewServer, qvsCluster.Trim());
                    if (qvs == null)
                    {
                        commSupport.PrintMessage("Could not find QlikView Server", true);
                    }
                    ServiceInfo qvwsService = FindService(client, ServiceTypes.QlikViewWebServer, qvwsMachine.Trim());
                    if (qvwsService == null)
                    {
                        commSupport.PrintMessage("Could not find Qvws", true);
                    }

                    string qvwsUrl = qvwsService.Address.Scheme + "://" + qvwsService.Address.Host;

                    links.Add("Name" + delimiter + "URL" + delimiter + "Description" + delimiter + "Space" + delimiter + "Type" + delimiter + "DateCreated");
                    client.ClearQVSCache(QVSCacheObjects.UserDocumentList);
                    List<DocumentNode> userDocs = client.GetUserDocuments(qvs.ID);
                    List<DocumentFolder> userDocFolders = client.GetUserDocumentFolders(qvs.ID, DocumentFolderScope.All);

                    bool filterForCategories = !string.IsNullOrEmpty(category);
                    foreach (DocumentNode userDoc in userDocs)
                    {
                        DocumentFolder docFolder = userDocFolders.FirstOrDefault(x => x.ID.Equals(userDoc.FolderID));
                        string mountName = string.Empty;
                        if (docFolder != null)
                        {
                            mountName = docFolder.General.Path.ToLower();
                        }
                        if (filterForCategories)
                        {
                            DocumentMetaData dmd = client.GetDocumentMetaData(userDoc, DocumentMetaDataScope.All);
                            if (dmd.DocumentInfo.Category.ToLower().Equals(category.ToLower()))
                            {
                                ComposeDocumentLinkUrl(mountName, userDoc.RelativePath, qvwsUrl, userDoc.Name, qvsCluster, space, "");
                            }
                        }
                        else
                        {
                            ComposeDocumentLinkUrl(mountName, userDoc.RelativePath, qvwsUrl, userDoc.Name, qvsCluster, space, "");
                        }
                    }
                    WriteToCSVFile(csvFileName);
                }
                else
                {
                    commSupport.PrintMessage("Could not create connection to QMS", true);
                }
            }
            catch (Exception e)
            {
                commSupport.PrintMessage("Exception when exporting links, run help command for information about usage: Exeception:" + e.Message, true);
            }
        }

        private void WriteToCSVFile(string csvFileName)
        {
            if (links.Count > 1)
            {
                File.WriteAllLines(csvFileName, links.ToArray(), System.Text.Encoding.UTF8);
                commSupport.PrintMessage(links.Count - 1 + " links exported to " + csvFileName, false);
            }
            else
            {
                commSupport.PrintMessage("No links extracted, no file created", false);
            }
        }

        private static ServiceInfo FindService(IQMS client, ServiceTypes serviceType, string serviceName)
        {
            List<ServiceInfo> service = client.GetServices(serviceType);
            if (service != null && service.Count > 0)
            {
                return service.FirstOrDefault(x => x.Name.Equals(serviceName));
            }
            return null;
        }

        private void ComposeDocumentLinkUrl(string mountPoint, string subFolder, string qvwsMachineName, string docName, string location, string space, string description)
        {
            string mount = mountPoint?.Trim();
            if (!string.IsNullOrEmpty(mount))
            {
                mount += "\\";
            }

            string sub = subFolder?.Trim();
            if (!string.IsNullOrEmpty(sub))
            {
                sub += "\\";
            }

            string documentLinkUrl =
                qvwsMachineName +
                "/QvAJAXZfc/opendoc.htm" +
                "?document=" + Uri.EscapeDataString(mount + sub + docName) +
                "&host=" + Uri.EscapeDataString(location);
            links.Add(Path.GetFileNameWithoutExtension(docName) + delimiter + documentLinkUrl + delimiter + description + delimiter + space + delimiter + "QlikView" + delimiter + DateTime.Now.ToShortDateString());
        }
    }
}
