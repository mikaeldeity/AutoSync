using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using AutoSync.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AutoSync
{
    public class App : IExternalApplication
    {
        //time always need to be multiples of checkinterval

        public static readonly int CheckInterval = 5 * 60;//5 min 

        public static readonly int DismissTime = 30 * 60;//30 min

        public static readonly int RetrySync = 30 * 60;//30 min

        public static readonly int RelinquishCheck = 15 * 60;//15 min

        public static readonly int SyncCheck = 120 * 60;//120 min

        public static int CountDown = 0;

        public static bool IsRunning = false;

        public static bool IsRelinquishing = false;

        public static bool IsSyncing = false;

        public static readonly HashSet<Document> Documents = new HashSet<Document>();

        private static readonly Timer IdleTimer = new Timer(Tick, null, CheckInterval * 1000, CheckInterval * 1000);
        private static readonly IdleTimerEvent TimerEvent = new IdleTimerEvent();
        private static ExternalEvent ExternalEvent = null;
        private void AddRibbonButton(UIControlledApplication uiapp)
        {
            RibbonPanel ribbonPanel = uiapp.CreateRibbonPanel("AutoSync");

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
            AddRibbonButton(uiapp);

            ExternalEvent = ExternalEvent.Create(TimerEvent);
            uiapp.ControlledApplication.DocumentOpening += new EventHandler<DocumentOpeningEventArgs>(OnOpening);
            uiapp.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(OnOpened);
            uiapp.ControlledApplication.DocumentCreated += new EventHandler<DocumentCreatedEventArgs>(OnCreated);
            uiapp.ControlledApplication.DocumentClosing += new EventHandler<DocumentClosingEventArgs>(OnClosing);
            uiapp.ControlledApplication.DocumentChanged += new EventHandler<DocumentChangedEventArgs>(OnChanged);
            uiapp.ViewActivated += new EventHandler<ViewActivatedEventArgs>(OnViewActivated);

            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication uiapp)
        {
            IdleTimer.Dispose();
            return Result.Succeeded;
        }
        internal void OnOpening(object sender, DocumentOpeningEventArgs e)
        {
            IsRunning = true;
        }
        internal void OnOpened(object sender, DocumentOpenedEventArgs e)
        {
            Reset();
            NewDocument(e.Document);
            IsRunning = false;
        }
        internal void OnClosing(object sender, DocumentClosingEventArgs e)
        {
            Reset();
            RemoveDocument(e.Document);
        }
        internal void OnCreated(object sender, DocumentCreatedEventArgs e)
        {
            Reset();
            NewDocument(e.Document);
        }
        public void OnChanged(object sender, DocumentChangedEventArgs e)
        {
            Reset();
        }
        internal void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            Reset();
        }
        private void NewDocument(Document doc)
        {
            if (doc.IsFamilyDocument | doc.IsDetached | !doc.IsWorkshared)
            {
                return;
            }
            Documents.Add(doc);
        }
        private static void RemoveDocument(Document doc)
        {
            if (Documents.Contains(doc))
            {
                Documents.Remove(doc);
            }
        }
        private static void Reset()
        {
            if (IsRunning) { return; }

            CountDown = 0;
            IsRelinquishing = false;
            IsSyncing = false;
        }
        private static void Tick(object state)
        {
            if (ExternalEvent == null | Documents.Count == 0)
            {
                Reset();
                return;
            }

            if (IsRunning | IsSyncing | IsRelinquishing)
            {
                return;
            }

            CountDown += CheckInterval;

            if (CountDown > SyncCheck)
            {
                IsSyncing = true;
                ExternalEvent.Raise();
            }
            else
            {
                if (CountDown % RelinquishCheck == 0)
                {
                    IsRelinquishing = true;
                    ExternalEvent.Raise();
                }
            }
        }
        private static bool Dismiss()
        {
            var dismissdialog = new DismissDialog(DismissTime / 60);

            var dialog = dismissdialog.ShowDialog();

            if (dialog == true)
            {
                CountDown -= DismissTime;
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
                if (Documents.Count == 0)
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

                try
                {
                    IsRunning = true;

                    if (IsRelinquishing)
                    {
                        foreach (Document doc in Documents)
                        {
                            try
                            {
                                if (doc != null && doc.IsValidObject)
                                {
                                    WorksharingUtils.RelinquishOwnership(doc, relinquishoptions, transact);
                                }
                            }
                            catch { }
                        }

                        IsRelinquishing = false;

                        Dismiss();
                    }

                    if (IsSyncing)
                    {
                        if (Dismiss())
                        {
                            IsSyncing = false;
                            return;
                        }

                        app.Application.FailuresProcessing += FailureProcessor;

                        foreach (Document doc in Documents)
                        {
                            try
                            {
                                if (doc != null && doc.IsValidObject)
                                {
                                    WorksharingUtils.RelinquishOwnership(doc, relinquishoptions, transact);
                                    doc.SynchronizeWithCentral(transact, syncset);
                                    CountDown = 0;
                                    app.Application.WriteJournalComment("AutoSync", true);
                                }
                            }
                            catch
                            {
                                CountDown -= RetrySync;
                            }
                        }

                        app.Application.FailuresProcessing -= FailureProcessor;

                        IsSyncing = false;
                    }
                }
                finally
                {
                    IsRunning = false;

                    transact.Dispose();
                    syncset.Dispose();
                    relinquishoptions.Dispose();
                }
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
            TaskDialog.Show("Status", $"AutoSync is active.\n\nRelinquish every {App.RelinquishCheck} minutes.\nSync after {App.SyncCheck} minutes.");

            return Result.Succeeded;
        }
    }
}
