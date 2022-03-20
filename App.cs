using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace AutoSync
{
    public class App : IExternalApplication
    {
        //time always need to be multiples of checkinterval
        private static readonly int checkinterval = 5;

        private static readonly int dismisstime = 30;

        private static readonly int retrysync = 30;

        internal static readonly int relinquishcheck = 15;

        internal static readonly int synccheck = 120;

        private static int countdown = 0;

        private static bool running = false;

        private static bool relinquish = false;

        private static bool sync = false;

        private static readonly List<Document> Documents = new List<Document>();

        private static readonly System.Threading.Timer IdleTimer = new System.Threading.Timer(Tick, null, checkinterval * 60 * 1000, checkinterval * 60 * 1000);

        private static readonly IdleTimerEvent TimerEvent = new IdleTimerEvent();

        private static ExternalEvent TimerExternalEvent;
        private void AddRibbonButton(UIControlledApplication a)
        {
            RibbonPanel ribbonPanel = a.CreateRibbonPanel("AutoSync");

            string assembly = Assembly.GetExecutingAssembly().Location;

            PushButtonData data = new PushButtonData("AutoSync", "AutoSync", assembly, "AutoSync.ShowActive")
            {
                AvailabilityClassName = "AutoSync.Availability"
            };
            PushButton button = ribbonPanel.AddItem(data) as PushButton;
            button.ToolTip = "Automatically Sync Inactive Revit Documents.";
            Uri uri = new Uri("pack://application:,,,/AutoSync;component/AutoSync.png");
            BitmapImage image = new BitmapImage(uri);
            button.LargeImage = image;
        }
        public Result OnStartup(UIControlledApplication uiapp)
        {
            TimerExternalEvent = ExternalEvent.Create(TimerEvent);

            uiapp.ControlledApplication.DocumentOpening += OnOpening;
            uiapp.ControlledApplication.DocumentOpened += OnOpened;
            uiapp.ControlledApplication.DocumentCreated += OnCreated;
            uiapp.ControlledApplication.DocumentClosing += OnClosing;
            uiapp.ControlledApplication.DocumentChanged += OnChanged;
            uiapp.ViewActivated += OnViewActivated;

            AddRibbonButton(uiapp);

            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication a)
        {
            IdleTimer.Dispose();
            return Result.Succeeded;
        }
        private void Reset()
        {
            countdown = 0;
            relinquish = false;
            sync = false;
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
            RemoveDocument(e.Document);
        }
        internal void OnCreated(object sender, DocumentCreatedEventArgs e)
        {
            NewDocument(e.Document);
        }
        public void OnChanged(object sender, DocumentChangedEventArgs e)
        {
            if (running) { return; }

            Document doc = e.GetDocument();

            if (Documents.Contains(doc))
            {
                Reset();
            }
        }
        internal void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            if (running) { return; }

            Document doc = e.Document;

            if (Documents.Contains(doc))
            {
                Reset();
            }
        }
        private void RemoveDocument(Document doc)
        {
            if (Documents.Contains(doc))
            {
                Documents.Remove(doc);
            }
        }
        private void NewDocument(Document doc)
        {
            if (doc.IsFamilyDocument | doc.IsDetached | !doc.IsWorkshared) { return; }

            if (!Documents.Contains(doc))
            {
                Documents.Add(doc);
            }

            Reset();
        }
        private static void Tick(object state)
        {
            if (TimerExternalEvent == null | Documents.Count == 0) { countdown = 0; return; }

            if (running | sync) { return; }

            countdown += checkinterval;

            if (countdown == relinquishcheck)
            {
                relinquish = true;
                TimerExternalEvent.Raise();
            }
            else if (countdown == synccheck)
            {
                sync = true;
                TimerExternalEvent.Raise();
            }
        }
        private static bool Dismiss()
        {
            var dismissdialog = new Dialogs.DismissDialog(dismisstime);

            if (dismissdialog.ShowDialog() == true)
            {
                countdown -= dismisstime * 60;
                return true;
            }
            else
            {
                return false;
            }
        }
        public class IdleTimerEvent : IExternalEventHandler
        {
            public void Execute(UIApplication app)
            {
                if (Documents.Count == 0) { return; }

                TransactWithCentralOptions transact = new TransactWithCentralOptions();
                SynchLockCallback transCallBack = new SynchLockCallback();
                transact.SetLockCallback(transCallBack);
                SynchronizeWithCentralOptions syncset = new SynchronizeWithCentralOptions();
                RelinquishOptions relinquishoptions = new RelinquishOptions(true)
                {
                    CheckedOutElements = true
                };
                syncset.SetRelinquishOptions(relinquishoptions);

                running = true;

                if (relinquish)
                {
                    foreach (Document doc in Documents)
                    {
                        try
                        {
                            WorksharingUtils.RelinquishOwnership(doc, relinquishoptions, transact);
                        }
                        catch { }
                    }

                    relinquish = false;
                }
                if (sync)
                {
                    if (Dismiss())
                    {
                        sync = false;
                        running = false;
                        return;
                    }

                    app.Application.FailuresProcessing += FailureProcessor;

                    bool syncfailed = false;

                    foreach (Document doc in Documents)
                    {
                        try
                        {
                            doc.SynchronizeWithCentral(transact, syncset);
                            app.Application.WriteJournalComment("AutoSync", true);
                        }
                        catch
                        {
                            syncfailed = true;
                        }
                    }

                    app.Application.FailuresProcessing -= FailureProcessor;

                    if (syncfailed)
                    {
                        countdown -= retrysync;
                    }

                    sync = false;
                }

                running = false;

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
    public class Availability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication a, CategorySet b)
        {
            return true;
        }
    }
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    class ShowActive : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("Status", $"AutoSync is active.\n\nRelinquish every {App.relinquishcheck} minutes.\nSync after {App.synccheck} minutes.");

            return Result.Succeeded;
        }
    }
}
