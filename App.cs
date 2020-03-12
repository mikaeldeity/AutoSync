using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace AutoSync
{
    class App : IExternalApplication
    {
        private static readonly int checkinterval = 1; //Seconds

        private static readonly int dismisstime = 5;

        private static readonly int retrysync = 5;

        private static readonly int relinquishcheck = 30;

        private static readonly int synccheck = 60;

        private static readonly int closecheck = 70;

        private static int countdown = 0;

        public static bool running = false;

        public static bool relinquish = false;

        public static bool sync = false;

        public static bool close = false;

        private static readonly Dictionary<Document, bool> docdict = new Dictionary<Document, bool>();

        private static readonly List<Document> doclist = new List<Document>();

        private static readonly System.Threading.Timer idletimer = new System.Threading.Timer(Tick, null, checkinterval*1000, checkinterval*1000);
        private static readonly IdleTimerEvent timerevent = new IdleTimerEvent();
        private static ExternalEvent externalevent = null;
        public Result OnStartup(UIControlledApplication a)
        {
            RibbonPanel ribbonPanel = a.CreateRibbonPanel("AutoSync");
            PushButtonData b1Data = new PushButtonData("BEAM", "BEAM", thisAssemblyPath, "REVITBEAM.BEAM");
            ddPushButton pb1 = sb.AddPushButton(b1Data);
            pb1.ToolTip = "Import Geometry from Rhino";
            BitmapImage pb1Image = new BitmapImage(new Uri("pack://application:,,,/REVITBEAM;component/Resources/beam.png"));
            pb1.LargeImage = pb1Image;

            externalevent = ExternalEvent.Create(timerevent);
            //a.Idling += OnIdling;
            a.ControlledApplication.DocumentOpening += new EventHandler<DocumentOpeningEventArgs>(OnOpening);
            a.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(OnOpened);
            a.ControlledApplication.DocumentCreated += new EventHandler<DocumentCreatedEventArgs>(OnCreated);
            a.ControlledApplication.DocumentClosing += new EventHandler<DocumentClosingEventArgs>(OnClosing);
            a.ControlledApplication.DocumentChanged += new EventHandler<DocumentChangedEventArgs>(OnChanged);
            a.ViewActivated += new EventHandler<ViewActivatedEventArgs>(OnViewActivated);
            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication a)
        {
            //a.Idling -= OnIdling;
            idletimer.Dispose();
            return Result.Succeeded;
        }
        internal void OnIdling(object sender, IdlingEventArgs e)
        {            
            //IDLING BROKEN          
        }
        internal void OnOpening(object sender, DocumentOpeningEventArgs e)
        {
            running = true;
        }
        internal void OnOpened(object sender, DocumentOpenedEventArgs e)
        {
            NewDocument(e.Document);
            running = false;
        }
        internal void OnClosing(object sender, DocumentClosingEventArgs e)
        {
            if (docdict.ContainsKey(e.Document))
            {
                docdict.Remove(e.Document);
            }
            else if (doclist.Contains(e.Document))
            {
                doclist.Remove(e.Document);
            }
        }
        internal void OnCreated(object sender, DocumentCreatedEventArgs e)
        {
            NewDocument(e.Document);
        }
        public void OnChanged(object sender, DocumentChangedEventArgs e)
        {
            if (running)
            {
                return;
            }

            Document doc = e.GetDocument();

            if (docdict.ContainsKey(doc))
            {
                countdown = 0;
                relinquish = false;
                sync = false;
                close = false;
            }
        }
        internal void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            if (running)
            {
                return;
            }

            Document doc = e.Document;

            if (docdict.ContainsKey(doc))
            {
                countdown = 0;
                relinquish = false;
                sync = false;
                close = false;
            }
        }
        private void NewDocument(Document doc)
        {
            if (doc.IsFamilyDocument | doc.IsDetached | !doc.IsWorkshared)
            {
                if (doclist.Contains(doc))
                {
                    doclist.Add(doc);
                }
                return;
            }

            countdown = 0;
            relinquish = false;
            sync = false;
            close = false;

            TaskDialog td = new TaskDialog("New Workshared Document")
            {
                MainInstruction = "You have opened a new Document.",
                MainContent = "How do you wish to continue when this Document is inactive for too long?"
            };

            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Sync then Close");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Close");

            switch (td.Show())
            {
                case TaskDialogResult.CommandLink1:
                    docdict.Add(doc, true);
                    break;

                case TaskDialogResult.CommandLink2:                    
                    docdict.Add(doc, false);
                    break;

                default:
                    docdict.Add(doc, true);
                    break;
            }
        }
        private static void Tick(object state)
        {
            if (externalevent == null | docdict.Keys.Count == 0)
            {
                countdown = 0;
                return;
            }            

            if (running | sync | close)
            {
                return;
            }

            countdown += checkinterval;

            if (countdown == relinquishcheck)
            {
                relinquish = true;
                externalevent.Raise();
            }
            else if (countdown == synccheck)
            {
                sync = true;
                externalevent.Raise();
            }
            else if (countdown == closecheck)
            {
                close = true;
                externalevent.Raise();
            }

            //WriteTxt(countdown.ToString() + " Docs: " + docdict.Keys.Count.ToString());
        }
        private static bool Dismiss()
        {
            var dismissdialog = new DismissDialog();

            var dialog = dismissdialog.ShowDialog();
            
            if (dialog == DialogResult.OK)
            {
                countdown -= dismisstime;
                return true;
            }
            else
            {
                return false;
            }
        }
        private static void WriteTxt(string time)
        {
            using (StreamWriter writer = new StreamWriter("C:\\Users\\stkelly\\Desktop\\New Text Document.txt"))
            {
                writer.WriteLine(time);
            }
        }
        public class IdleTimerEvent : IExternalEventHandler
        {
            public void Execute(UIApplication app)
            {
                if (App.docdict.Keys.Count == 0)
                {
                    return;
                }                                

                TransactWithCentralOptions transact = new TransactWithCentralOptions();
                SynchLockCallback transCallBack = new SynchLockCallback();
                transact.SetLockCallback(transCallBack);
                SynchronizeWithCentralOptions syncset = new SynchronizeWithCentralOptions();
                RelinquishOptions relinquishoptions = new RelinquishOptions(true)
                {
                    CheckedOutElements = true
                };
                syncset.SetRelinquishOptions(relinquishoptions);

                App.running = true;

                if (relinquish)
                {
                    foreach (Document doc in App.docdict.Keys)
                    {
                        //WriteTxt("relinquishing " + doc.Title);
                        try
                        {
                            WorksharingUtils.RelinquishOwnership(doc, relinquishoptions, transact);
                        }
                        catch (Exception e)
                        {
                            //WriteTxt(e.Message);
                        }                        
                    }

                    relinquish = false;
                }
                if (sync)
                {
                    if (App.Dismiss())
                    {                        
                        sync = false;
                        App.running = false;
                        return;
                    }

                    app.Application.FailuresProcessing += FailureProcessor;

                    bool syncfailed = false;

                    foreach (Document doc in App.docdict.Keys)
                    {
                        try
                        {
                            if (docdict[doc])
                            {
                                //WriteTxt("syncing " + doc.Title);
                                doc.SynchronizeWithCentral(transact, syncset);
                                app.Application.WriteJournalComment("Document Auto Synced", true);
                            }
                        }
                        catch (Exception e)
                        {
                            syncfailed = true;
                            //WriteTxt(e.Message);
                        }
                    }

                    app.Application.FailuresProcessing -= FailureProcessor;

                    if (syncfailed)
                    {
                        countdown -= retrysync;
                    }

                    sync = false;
                }
                if (close)
                {
                    if (App.Dismiss())
                    {
                        App.running = false;
                        close = false;
                        return;
                    }

                    bool closelast = false;

                    string activepathname = app.ActiveUIDocument.Document.PathName;

                    List<Document> docsdeletelist = new List<Document>();

                    foreach (Document doc in App.docdict.Keys)
                    {
                        if (activepathname == doc.PathName)
                        {                            
                            closelast = true;
                        }
                        else
                        {
                            docsdeletelist.Add(doc);
                        }                                             
                    }
                    foreach (Document doc in docsdeletelist)
                    {
                        try
                        {
                            //WriteTxt("Closing " + doc.Title);
                            doc.Close(false);
                        }
                        catch (Exception e)
                        {
                            //WriteTxt(e.Message);
                        }
                    }                    

                    if (closelast)
                    {
                        RevitCommandId closeDoc = RevitCommandId.LookupPostableCommandId(PostableCommand.Close);
                        app.PostCommand(closeDoc);
                    }

                    close = false;
                    countdown = 0;
                }

                App.running = false;

                transact.Dispose();
                syncset.Dispose();
                relinquishoptions.Dispose();               
            }
            public string GetName()
            {
                return "AutoSync";
            }
            private void FailureProcessor(object sender, FailuresProcessingEventArgs e)
            {
                FailuresAccessor fas = e.GetFailuresAccessor();

                List<FailureMessageAccessor> fma = fas.GetFailureMessages().ToList();

                foreach (FailureMessageAccessor fa in fma)
                {
                    string failuremessage = fa.GetDescriptionText();

                    fas.DeleteWarning(fa);
                }
            }
        }
        class SynchLockCallback : ICentralLockedCallback
        {            
            public bool ShouldWaitForLockAvailability()
            {
                return false;
            }

        }
    }
}
