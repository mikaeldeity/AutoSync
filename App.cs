using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
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
        internal static Settings Settings = new Settings();

        private static readonly int CheckInterval = 5;

        private static readonly int DismissTime = 30;

        private static readonly int RetrySync = 30;

        private static int CountDown = 0;

        private static bool Running = false;

        private static bool Relinquish = false;

        private static bool Sync = false;

        private static readonly List<Document> Documents = new List<Document>();

        private static readonly Timer IdleTimer = new Timer(Tick, null, CheckInterval * 60 * 1000, CheckInterval * 60 * 1000);

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
            CountDown = 0;
            Relinquish = false;
            Sync = false;
        }
        internal void OnOpening(object sender, DocumentOpeningEventArgs e)
        {
            Running = true;
        }
        internal void OnOpened(object sender, DocumentOpenedEventArgs e)
        {
            NewDocument(e.Document);
            Running = false;
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
            if (Running) { return; }

            Document doc = e.GetDocument();

            if (Documents.Contains(doc))
            {
                Reset();
            }
        }
        internal void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            if (Running) { return; }

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
            if (TimerExternalEvent == null | Documents.Count == 0) { CountDown = 0; return; }

            if (Running | Settings.Sync) { return; }

            CountDown += CheckInterval;

            if (CountDown == Settings.RelinquishCheck && Settings.Relinquish)
            {
                Relinquish = true;
                TimerExternalEvent.Raise();
            }
            else if (CountDown == Settings.SyncCheck && Settings.Sync)
            {
                Sync = true;
                TimerExternalEvent.Raise();
            }
        }
        private static bool Dismiss()
        {
            var dismissdialog = new Dialogs.DismissDialog(DismissTime);

            if (dismissdialog.ShowDialog() == true)
            {
                CountDown -= DismissTime * 60;
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
                Running = true;

                try
                {
                    app.Application.FailuresProcessing += FailureProcessor;

                    if (Documents.Count == 0) { return; }

                    TransactWithCentralOptions transactionoptions = new TransactWithCentralOptions();
                    SynchLockCallback callback = new SynchLockCallback();
                    transactionoptions.SetLockCallback(callback);
                    SynchronizeWithCentralOptions syncoptions = new SynchronizeWithCentralOptions();
                    RelinquishOptions relinquishoptions = new RelinquishOptions(true)
                    {
                        CheckedOutElements = true
                    };
                    syncoptions.SetRelinquishOptions(relinquishoptions);

                    if (Relinquish)
                    {
                        foreach (Document doc in Documents)
                        {
                            try
                            {
                                WorksharingUtils.RelinquishOwnership(doc, relinquishoptions, transactionoptions);
                            }
                            catch { }
                        }

                        Relinquish = false;
                    }

                    if (Sync)
                    {
                        if (Dismiss())
                        {
                            Sync = false;
                            Running = false;
                            return;
                        }

                        foreach (Document doc in Documents)
                        {
                            try
                            {
                                doc.SynchronizeWithCentral(transactionoptions, syncoptions);
                                app.Application.WriteJournalComment("AutoSync", true);
                            }
                            catch
                            {
                                CountDown -= RetrySync;
                            }
                        }

                        Sync = false;
                    }

                    transactionoptions.Dispose();
                    syncoptions.Dispose();
                    relinquishoptions.Dispose();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
                finally
                {
                    app.Application.FailuresProcessing -= FailureProcessor;
                    Running = false;
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
            TaskDialog.Show("Status", $"AutoSync is active.\n\nRelinquish every {App.Settings.RelinquishCheck} minutes.\nSync after {App.Settings.SyncCheck} minutes.");

            return Result.Succeeded;
        }
    }
}
