﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Qiqqa.Common.Configuration;
using Qiqqa.DocumentLibrary.BundleLibrary;
using Qiqqa.DocumentLibrary.IntranetLibraryStuff;
using Utilities;
using Utilities.Files;
using Utilities.GUI;
using Utilities.Misc;
using Utilities.Shutdownable;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


namespace Qiqqa.DocumentLibrary.WebLibraryStuff
{
    internal class WebLibraryManager
    {
        private static WebLibraryManager __instance = null;
        private static object instance_lock = new object();

        public static WebLibraryManager Instance
        {
            get
            {
                // Utilities.LockPerfTimer l2_clk = Utilities.LockPerfChecker.Start();
                lock (instance_lock)
                {
                    // l2_clk.LockPerfTimerStop();

                    if (null == __instance)
                    {
                        __instance = new WebLibraryManager();
                    }
                    return __instance;
                }
            }
        }

        // NOTE: we assume access to a bool is atomic: 
        // write and read can occur simulataneously in multiple threads. 
        // When it's not atomic, no harm done -- we wait until the bugger turns FALSE.
        private volatile bool holdoffUntilKicked;        

        // Making sure the WebLibraryManager is alive and kicking, if it hasn't already been instantiated previously.
        //
        // IFF it has been instatiated *before*, that's not a problem, but *we* know there's a possibility the UPGRADE
        // process is still running then, so we'll have to wait until all upgrade activities have completed before
        // we kick off our own activities.
        // That's what this `Kick()` API is good for: it's the signal to this WebLibraryManager that we're off to the races finally!
        public void Kick()
        {
            // kick the instance off the waiting stand:
            holdoffUntilKicked = false;
        }

        [Conditional("DEBUG")]
        public static void AssertInitIsDone()
        {
            // don't use lock around this check as we're particularly interested in code which runs in parallel to a locked Init() which is still running!
            ASSERT.Test(__instance != null);
        }

        private Dictionary<string, WebLibraryDetail> web_library_details = new Dictionary<string, WebLibraryDetail>();
        private WebLibraryDetail guest_web_library_detail;

        public delegate void WebLibrariesChangedDelegate();
        public event WebLibrariesChangedDelegate WebLibrariesChanged;

        private WebLibraryManager()
        {
            holdoffUntilKicked = true;

            // WARNING: this code is executed inside an Instance lock (lock_instance) and should therefor be both minimal and FAST:
            // hence we push all the work to be done onto a worker thread for processing at a later time.
            SafeThreadPool.QueueAsyncUserWorkItem(async () =>
            {
                WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

                // Whoa! wait until we get *kicked*, for otherwise we MAY try to access libraries which are still
                // in the process of being *upgraded* via DoUpgrade()!
                Logging.Info("Waiting for the UPGRADE startup process to finish.");
                while (holdoffUntilKicked)
                {
                    if (ShutdownableManager.Instance.IsShuttingDown)
                    {
                        return;
                    }
                    await Task.Delay(250);
                }
                Logging.Info("Done waiting for the UPGRADE startup process to finish: going to load the libraries...");

                // Look for any web libraries that we know about
                LoadKnownWebLibraries(KNOWN_WEB_LIBRARIES_FILENAME, only_load_those_libraries_which_are_actually_present: false);

                // *************************************************************************************************************
                // *** MIGRATION TO OPEN SOURCE CODE ***************************************************************************
                // *************************************************************************************************************
                AddLegacyWebLibrariesThatCanBeFoundOnDisk();
                // *************************************************************************************************************

                AddLocalGuestLibraryIfMissing();

                InitAllLoadedLibraries();

                ImportManualsIntoLocalGuestLibraryIfMissing();

                SaveKnownWebLibraries();

                StatusManager.Instance.ClearStatus("LibraryInitialLoad");

                // Fire the 'all done' event which signals the UI that the libraries have finally been loaded:
                FireWebLibrariesChanged();
            });
        }

        private void FireWebLibrariesChanged()
        {
            Logging.Info("+Notifying everyone that web libraries have changed");
            WebLibrariesChanged?.Invoke();
            Logging.Info("-Notifying everyone that web libraries have changed");
        }


        // *************************************************************************************************************
        // *** MIGRATION TO OPEN SOURCE CODE ***************************************************************************
        // *************************************************************************************************************
        private void AddLegacyWebLibrariesThatCanBeFoundOnDisk()
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            try
            {
                ConfigurationManager.ThrowWhenActionIsNotEnabled(nameof(AddLegacyWebLibrariesThatCanBeFoundOnDisk));

                /**
                 * Plan:
                 * - Iterate through all the folders in the Qiqqa data directory.
                 * - If a folder contains a valid Library record and it is a WEB library,
                 *   then add it to our list with the word '[LEGACY]' in front of it.
                 */

                string base_directory_path = ConfigurationManager.Instance.BaseDirectoryForQiqqa;
                Logging.Info("Going to scan for web libraries at: {0}", base_directory_path);
                if (Directory.Exists(base_directory_path))
                {
                    string[] library_directories = Directory.GetDirectories(base_directory_path);
                    foreach (string library_directory in library_directories)
                    {
                        Logging.Info("Inspecting directory {0} - Phase 1 : Web & Known Libraries", library_directory);

                        string databaselist_file = Path.GetFullPath(Path.Combine(library_directory, @"Qiqqa.known_web_libraries"));
                        if (File.Exists(databaselist_file))
                        {
                            LoadKnownWebLibraries(databaselist_file, only_load_those_libraries_which_are_actually_present: true);
                        }
                    }

                    foreach (string library_directory in library_directories)
                    {
                        Logging.Info("Inspecting directory {0} - Phase 2 : Intranet Libraries", library_directory);

                        string databaselist_file = IntranetLibraryTools.GetLibraryDetailPath(library_directory);
                        if (File.Exists(databaselist_file))
                        {
                            IntranetLibraryDetail intranet_library_detail = IntranetLibraryDetail.Read(databaselist_file);

                            UpdateKnownWebLibraryFromIntranet(library_directory, extra_info_message_on_skip: String.Format(" as obtained from file {0}", databaselist_file));
                        }
                    }

                    foreach (string library_directory in library_directories)
                    {
                        Logging.Info("Inspecting directory {0} - Phase 3 : Bundles", library_directory);

                        // must be a qiqqa_bundle and/or qiqqa_bundle_manifest file set
                        Logging.Warn("Auto bundle import at startup is not yet supported.");
                    }

                    foreach (string library_directory in library_directories)
                    {
                        Logging.Info("Inspecting directory {0} - Phase 4 : Local and Legacy Libraries", library_directory);

                        string database_file = LibraryDB.GetLibraryDBPath(library_directory);
                        string db_syncref_path = IntranetLibraryTools.GetLibraryMetadataPath(library_directory);

                        // add/update only if this is not a Internet sync directory/DB!
                        if (File.Exists(db_syncref_path))
                        {
                            Logging.Info("Skip the Qiqqa Internet/Intranet Sync directory and the sync DB contained therein: '{0}'", db_syncref_path);

                            // https://github.com/jimmejardine/qiqqa-open-source/issues/145 :: delete lib file when it is very small and was illegally
                            // constructed by a previous v82beta Qiqqa release:
                            if (File.Exists(database_file))
                            {
                                long s3length = File.GetSize(database_file);
                                if (6 * 1024 > s3length)
                                {
                                    Logging.Warn("DELETE the wrongfully created DB file '{0}' in the Qiqqa Internet/Intranet Sync directory and the sync DB contained therein: '{1}', which has precedence!", database_file, db_syncref_path);

                                    FileTools.DeleteToRecycleBin(database_file);
                                }
                                else
                                {
                                    Logging.Error("Inspect the Library DB file '{0}' in the Qiqqa Internet/Intranet Sync directory and the sync DB contained therein: '{1}', which MAY have precedence. Delete one of these manually to clean up your system as Qiqqa heuristics cannot tell which is the prevalent metadata database here!", database_file, db_syncref_path);
                                }
                            }

                            continue;
                        }
                        if (File.Exists(database_file))
                        {
                            var library_id = Path.GetFileName(library_directory);

                            WebLibraryDetail new_web_library_detail = new WebLibraryDetail();

                            new_web_library_detail.Id = library_id;
                            new_web_library_detail.Title = "Legacy Web Library - " + new_web_library_detail.Id;
                            // library: Intranet/Local type

                            UpdateKnownWebLibrary(new_web_library_detail);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem while scanning for (legacy) libraries.");
            }
        }

        // *************************************************************************************************************

        public void InitAllLoadedLibraries()
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            foreach (var pair in web_library_details)
            {
                WebLibraryDetail web_lib = pair.Value;
                Library library = web_lib.Xlibrary;
                // only set up a Library instance when there isn't already one.
                // `library` may already have been set up in a previous call to this API
                // at application start; *this* call therefor MUST be due to the user
                // CREATING or JOINING another Intranet Library then!
                if (library == null)
                {
                    if (ShutdownableManager.Instance.IsShuttingDown)
                    {
                        Logging.Info("InitAllLoadedLibraries: Breaking out of library loading loop due to application termination");
                        break;
                    }

                    library = web_lib.Xlibrary = new Library(web_lib);
                }
                else
                {
                    Logging.Info($"InitAllLoadedLibraries: Initializing the local library for library Id: '{web_lib.Id}', Title: '{web_lib.Title}'");
                }
                library.BuildFromDocumentRepository(web_lib);
            }
        }

        // *************************************************************************************************************

        private void AddLocalGuestLibraryIfMissing()
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            // Check if we have an existing Guest library
            foreach (var pair in web_library_details)
            {
                if (pair.Value.Id == "Guest")
                {
                    guest_web_library_detail = pair.Value;
                    break;
                }
            }

            // If we did not have a guest library, create one...
            if (null == guest_web_library_detail)
            {
                WebLibraryDetail new_web_library_detail = new WebLibraryDetail();
                new_web_library_detail.Id = "Guest";
                new_web_library_detail.Title = "Local Guest Library";
                new_web_library_detail.Description = "This is the library that comes with your Qiqqa guest account.";
                new_web_library_detail.Deleted = false;

                UpdateKnownWebLibrary(new_web_library_detail);

                // Store this reference to guest
                guest_web_library_detail = new_web_library_detail;
            }
        }

        // *************************************************************************************************************

        private void ImportManualsIntoLocalGuestLibraryIfMissing()
        {
            if (Runtime.IsRunningInVisualStudioDesigner) return;

            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            // Import the Qiqqa manuals in the background, waiting until the library has loaded...
            SafeThreadPool.QueueAsyncUserWorkItem(async () =>
            {
                while (!WebLibraryDetail.LibraryIsLoaded(guest_web_library_detail))
                {
                    if (ShutdownableManager.Instance.IsShuttingDown)
                    {
                        return;
                    }
                    await Task.Delay(500);
                }

                QiqqaManualTools.AddManualsToLibrary(guest_web_library_detail);
            });
        }

        public WebLibraryDetail Library_Guest => guest_web_library_detail;

        // *************************************************************************************************************

        public void UnloadAllLibraries()
        {
            //WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

#if true
            foreach (var pair in web_library_details)
            {
                WebLibraryDetail web_lib = pair.Value;
                Library library = web_lib.Xlibrary;
                if (library != null)
                {
                    web_lib.Xlibrary = null;
                    library.Dispose();
                }
            }
#endif

            // Allow the GC to collect these naturally.
            // Only when there are still dependency cycles or other stuff active, would this
            // nuking of the references to the libraries NOT help to release their memory.
            web_library_details = new Dictionary<string, WebLibraryDetail>();
            // DO NOT forget to also release the reference to the Guest library:
            guest_web_library_detail = null;

            GC.WaitForPendingFinalizers();
            GC.Collect(100, GCCollectionMode.Forced, true, true);
            Logging.Info("UnloadAllLibraries: Heap after forced GC compacting at the end: {0}", GC.GetTotalMemory(false));
        }

        // *************************************************************************************************************

        /// <summary>
        /// Returns all working web libraries, including the guest library.
        /// </summary>
        public List<WebLibraryDetail> WebLibraryDetails_WorkingWebLibraries
        {
            get
            {
                List<WebLibraryDetail> details = new List<WebLibraryDetail>();
                foreach (WebLibraryDetail wld in WebLibraryDetails_All_IncludingDeleted)
                {
                    if (!wld.Deleted)
                    {
                        details.Add(wld);
                    }
                }

                ASSERT.Test(details.Count > 0);

                return details;
            }
        }

        public List<WebLibraryDetail> WebLibraryDetails_All_IncludingDeleted
        {
            get
            {
                List<WebLibraryDetail> details = new List<WebLibraryDetail>();
                details.AddRange(web_library_details.Values);
                return details;
            }
        }

        public WebLibraryDetail GetLibrary(string library_id)
        {
            WebLibraryDetail web_library_detail;
            if (web_library_details.TryGetValue(library_id, out web_library_detail))
            {
                return web_library_detail;
            }
            else
            {
                return null;
            }
        }

        public void WaitForLibraryToLoad(WebLibraryDetail web_library_detail, Action exec_after_load)
        {
            // XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
            // TBD TBD TBD TBD TBD TBD
        }

        public void NotifyOfChangeToWebLibraryDetail()
        {
            SafeThreadPool.QueueUserWorkItem(() =>
            {
                SaveKnownWebLibraries();
            },
            skip_task_at_app_shutdown: false);
        }

        public void SortWebLibraryDetailsByLastAccessed(List<WebLibraryDetail> web_library_details)
        {
            string last_open_ordering = ConfigurationManager.Instance.ConfigurationRecord.GUI_LastSelectedLibraryId;

            // Is there nothing to do?
            if (String.IsNullOrEmpty(last_open_ordering))
            {
                return;
            }

            web_library_details.Sort(
                delegate (WebLibraryDetail a, WebLibraryDetail b)
                {
                    if (a == b) return 0;

                    // make sure sort criteria are consistent across the board or you'll get a BadComparer exception!
                    // Hence we MUST compare (B && !A) before (A), not just simply, and faster, (B) then (A):

                    if (b.Deleted && !a.Deleted) return -1;
                    if (a.Deleted) return +1;

                    if (b.Id == "Guest" && a.Id != "Guest") return -1;
                    if (a.Id == "Guest") return +1;

                    int pos_b = last_open_ordering.IndexOf(b.Id);
                    int pos_a = last_open_ordering.IndexOf(a.Id);
                    if (-1 == pos_b && -1 != pos_a) return -1;
                    if (-1 == pos_a) return +1;

                    return Sorting.Compare(pos_a, pos_b);
                }
            );
        }

        #region --- Known web library management -------------------------------------------------------------------------------------------------------------------------

        public static string KNOWN_WEB_LIBRARIES_FILENAME => Path.GetFullPath(Path.Combine(ConfigurationManager.Instance.BaseDirectoryForUser, @"Qiqqa.known_web_libraries"));

        private void LoadKnownWebLibraries(string filename, bool only_load_those_libraries_which_are_actually_present)
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            Logging.Info("+Loading known Web Libraries");
            try
            {
                if (File.Exists(filename))
                {
                    ConfigurationManager.ThrowWhenActionIsNotEnabled(nameof(LoadKnownWebLibraries));

                    KnownWebLibrariesFile known_web_libraries_file = SerializeFile.ProtoLoad<KnownWebLibrariesFile>(filename);
                    if (null != known_web_libraries_file.web_library_details)
                    {
                        foreach (WebLibraryDetail new_web_library_detail in known_web_libraries_file.web_library_details)
                        {
                            Logging.Info("We have known details for library '{0}' ({1})", new_web_library_detail.Title, new_web_library_detail.Id);

                            if (!new_web_library_detail.IsPurged)
                            {
                                string libdir_path = WebLibraryDetail.GetLibraryBasePathForId(new_web_library_detail.Id);
                                string libfile_path = LibraryDB.GetLibraryDBPath(libdir_path);

                                if (File.Exists(libfile_path) || !only_load_those_libraries_which_are_actually_present)
                                {
                                    UpdateKnownWebLibrary(new_web_library_detail);
                                }
                                else
                                {
                                    Logging.Info("Not loading library {0} with Id {1} as it does not exist on disk. (library file path: {2})", new_web_library_detail.Title, new_web_library_detail.Id, libfile_path);
                                }
                            }
                            else
                            {
                                Logging.Info("Not loading purged library {0} with id {1}.", new_web_library_detail.Title, new_web_library_detail.Id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem loading the known Web Libraries from config file {0}", filename);
            }
            Logging.Info("-Loading known Web Libraries");
        }

        private void SaveKnownWebLibraries(string filename = null)
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            if (null == filename)
            {
                filename = KNOWN_WEB_LIBRARIES_FILENAME;
            }

            Logging.Info("+Saving known Web Libraries to {0}", filename);

            try
            {
                // do NOT save to disk when ANY of the DEV/TEST settings tweak the default Qiqqa behaviour:
                ConfigurationManager.ThrowWhenActionIsNotEnabled(nameof(SaveKnownWebLibraries));
                ConfigurationManager.ThrowWhenActionIsNotEnabled(nameof(LoadKnownWebLibraries));
                ConfigurationManager.ThrowWhenActionIsNotEnabled(nameof(AddLegacyWebLibrariesThatCanBeFoundOnDisk));

                KnownWebLibrariesFile known_web_libraries_file = new KnownWebLibrariesFile();
                known_web_libraries_file.web_library_details = new List<WebLibraryDetail>();
                foreach (WebLibraryDetail web_library_detail in web_library_details.Values)
                {
                    known_web_libraries_file.web_library_details.Add(web_library_detail);
                }
                SerializeFile.ProtoSave<KnownWebLibrariesFile>(filename, known_web_libraries_file);
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem saving the known web libraries to file {0}", filename);
            }

            Logging.Info("-Saving known Web Libraries to {0}", filename);
        }

        private delegate int MixCollisionComparer(string x, string y);

        private static int DefaultMixCollisionComparer(string a, string b)
        {
            return (a.Length != b.Length ? a.Length - b.Length : a.CompareTo(b));
        }

        private static int PathsMixCollisionComparer(string a, string b)
        {
            if (a == b)
            {
                return 0;
            }

            // check which of them exists: that one wins
            if (Directory.Exists(a) || File.Exists(a))
            {
                if (!Directory.Exists(b) && !File.Exists(b))
                {
                    return -1;
                }
                else
                {
                    Logging.Warn("Both paths exist: '{0}' and '{1}'; taking the longest one: '{2}'.", a, b, (b.CompareTo(a) > 0 ? b : a));
                    return b.CompareTo(a);
                }
            }
            else
            {
                if (Directory.Exists(b) || File.Exists(b))
                {
                    return 1;
                }
                else
                {
                    Logging.Warn("Both paths DO NOT exist: '{0}' and '{1}'; taking the longest one: '{2}'.", a, b, (b.CompareTo(a) > 0 ? b : a));
                    return b.CompareTo(a);
                }
            }
        }

        private static string MixOldAndNew(string old, string fresh, string spec, ref int state, MixCollisionComparer compare = null)
        {
            // state: bit 0: prefer to use old, bit 1: prefer to use fresh, bit 2: used old, bit 3: used fresh, 0: don't know yet.
            old = old?.Trim();
            fresh = fresh?.Trim();
            if (old == fresh)
            {
                // same = keep
                return old;
            }
            if (String.IsNullOrEmpty(fresh))
            {
                // nothing to see here, nothing in the new one. Keep old as is.
                state |= 0x04;
                return old;
            }
            if (String.IsNullOrEmpty(old))
            {
                // old doesn't have this, while the new one has. Take fresh.
                state |= 0x08;
                return fresh;
            }

            if (old.StartsWith("Legacy Web Library - ") && !old.StartsWith("Legacy Web Library - "))
            {
                // old has (very probably un-edited) auto-generated title, while the new one has not. Take fresh.
                state |= 0x08;
                return fresh;
            }
            else if (!old.StartsWith("Legacy Web Library - ") && old.StartsWith("Legacy Web Library - "))
            {
                // new has (very probably un-edited) auto-generated title, while the old one has not. Take old.
                state |= 0x04;
                return old;
            }

            // when we get here, there's a collision between two different non-null values:
            if ((state & 0x03) == 0)
            {
                // no decision has been made yet, so we decide on Length of the string, after preprocessing to kill some useless chunks:
                string a = old.Replace("UNKNOWN", "").Replace("INTRANET_", "").Replace("Legacy Web Library - ", "");
                string b = fresh.Replace("UNKNOWN", "").Replace("INTRANET_", "").Replace("Legacy Web Library - ", "");
                // strip off GUIDs as well, as those will be part of the auto-generated titles:
                a = Regex.Replace(a, @"[0-9A-F]{8}-(?:[0-9A-F]{4}-){3}[0-9A-F]{12}", " ", RegexOptions.IgnoreCase);
                b = Regex.Replace(b, @"[0-9A-F]{8}-(?:[0-9A-F]{4}-){3}[0-9A-F]{12}", " ", RegexOptions.IgnoreCase);
                a = a.Trim();
                b = b.Trim();

                if (!String.IsNullOrEmpty(a) || !String.IsNullOrEmpty(b))
                {
                    // after stripping down, there still is some meat on deez bones...
                    if (String.IsNullOrEmpty(b))
                    {
                        // nothing to see here, nothing in the new one. Keep old as is.
                        Logging.Debug特("library info:: {0}: taking old STRIPPED value: '{1}' (unstripped old was: '{2}' & new: '{3}')", spec, a, old, fresh);
                        state |= 0x04;
                        return a;
                    }
                    if (String.IsNullOrEmpty(a))
                    {
                        // old doesn't have this, while the new one has. Take fresh.
                        Logging.Debug特("library info:: {0}: taking new STRIPPED value: '{1}' (unstripped old was: '{2}' & new: '{3}')", spec, b, old, fresh);
                        state |= 0x08;
                        return b;
                    }
                    if (a == b)
                    {
                        // same = take stripped-down any
                        //
                        // Apparently both had different cruft, but were otherwise the same, or one had cruft while the other hadn't.
                        // Either way, they're the same so there's no collision, hence no decision to make yet!
                        state |= 0x04;
                        return a;
                    }
                }
                else
                {
                    // both are empty after stripped down, hence they both carry cruft. Do we care which? Nah, we always ride with the latest:
                    Logging.Debug特("library info:: {0}: taking new CRUFT value: '{1}' (unstripped old was: '{2}')", spec, fresh, old);
                    state |= 0x08;
                    return fresh;
                }

                if (compare == null)
                {
                    compare = DefaultMixCollisionComparer;
                }
                bool decision = (compare(a, b) > 0);

                if (decision)
                {
                    state |= 0x02;   // go with the new one

                    if (!String.IsNullOrEmpty(b))
                    {
                        Logging.Debug特("library info:: {0}: taking new STRIPPED value: '{1}' (unstripped old was: '{2}' & new: '{3}')", spec, b, old, fresh);
                        state |= 0x08;
                        return b;
                    }
                }
                else
                {
                    state |= 0x01;   // go with the old one

                    if (!String.IsNullOrEmpty(a))
                    {
                        Logging.Debug特("library info:: {0}: taking old STRIPPED value: '{1}' (unstripped old was: '{2}' & new: '{3}')", spec, a, old, fresh);
                        state |= 0x04;
                        return a;
                    }
                }
            }
            // now resolve the collision:
            if ((state & 0x01) != 0)
            {
                Logging.Debug特("library info:: {0}: keeping old value: '{1}' (new was: '{2}')", spec, old, fresh);
                state |= 0x05;  // prefer old from now on
                return old;
            }
            Logging.Debug特("library info:: {0}: taking new value: '{1}' (old was: '{2}')", spec, fresh, old);
            state |= 0x0a;  // prefer new from now on
            return fresh;
        }

        public static readonly DateTime DATE_ZERO = new DateTime(1990, 1, 1);   // no Qiqqa existed before this date

        private static DateTime? MixOldAndNew(DateTime? old, DateTime? fresh, string spec, ref int state)
        {
            // state: bit 0: prefer to use old, bit 1: prefer to use fresh, bit 2: used old, bit 3: used fresh, 0: don't know yet.
            if (old == null && fresh == null)
            {
                // same = keep
                return old;
            }
            DateTime a = old ?? DATE_ZERO;
            DateTime b = fresh ?? DATE_ZERO;
            if (a.CompareTo(b) == 0)
            {
                // same = keep
                return old;
            }
            if (b <= DATE_ZERO)
            {
                // nothing to see here, nothing in the new one. Keep old as is.
                state |= 0x04;
                return old;
            }
            if (a <= DATE_ZERO)
            {
                // old doesn't have this, while the new one has. Take fresh.
                state |= 0x08;
                return fresh;
            }
            // when we get here, there's a collision between two different non-null values:
            if ((state & 0x03) == 0)
            {
                // no decision has been made yet, so we decide on most recent date:
                if (a < b)
                {
                    state |= 0x02;
                }
                else
                {
                    state |= 0x01;
                }
            }
            // now resolve the collision:
            if ((state & 0x01) != 0)
            {
                Logging.Debug特("library info:: {0}: keeping old value: '{1}' (new was: '{2}')", spec, old, fresh);
                state |= 0x05;  // prefer old from now on
                return old;
            }
            Logging.Debug特("library info:: {0}: taking new value: '{1}' (old was: '{2}')", spec, fresh, old);
            state |= 0x0a;  // prefer new from now on
            return fresh;
        }

        private static bool MixOldAndNew(bool old, bool fresh, string spec, ref int state)
        {
            // state: bit 0: prefer to use old, bit 1: prefer to use fresh, bit 2: used old, bit 3: used fresh, 0: don't know yet.
            if (old == fresh)
            {
                // same = keep
                return old;
            }

            // we don't mind about boolean data: it's too little to decide a collision resolution on.
            //
            // if any bit is set, we return true, else it's false. We don't change the state for this tidbit either.
            return old || fresh;
        }

        private void UpdateKnownWebLibrary(WebLibraryDetail new_web_library_detail, bool suppress_flush_to_disk = true, string extra_info_message_on_skip = "")
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            WebLibraryDetail old;

            if (web_library_details.TryGetValue(new_web_library_detail.Id, out old))
            {
                bool use_old = true;
                bool use_new = true;

                // don't work with the old find if it has been purged
                if (old.IsPurged)
                {
                    use_old = false;
                }
                // don't work with the new find if it has been purged
                if (new_web_library_detail.IsPurged)
                {
                    use_new = false;
                }
                // don't work with the old find if it has been deleted and the new one has not:
                if (old.Deleted && !new_web_library_detail.Deleted && use_new)
                {
                    use_old = false;
                }
                // and vice versa:
                if (!old.Deleted && new_web_library_detail.Deleted && use_old)
                {
                    use_new = false;
                }

                // skip when there's nothing new to pick up:
                if (!use_new)
                {
                    return;
                }
                // only do the mix/collision resolution when both old and new are viable:
                if (use_old)
                {
                    int state = 0x00; // bit 0: prefer to use old, bit 1: prefer to use fresh, bit 2: used old, bit 3: used fresh, 0: don't know yet.

                    // if there's already an entry present, see if we should 'upgrade' the info or stick with what we have:
                    old.LastBundleManifestDownloadTimestampUTC = MixOldAndNew(old.LastBundleManifestDownloadTimestampUTC, new_web_library_detail.LastBundleManifestDownloadTimestampUTC, new_web_library_detail.Id + "::" + nameof(old.LastBundleManifestDownloadTimestampUTC), ref state);
                    old.LastBundleManifestIgnoreVersion = MixOldAndNew(old.LastBundleManifestIgnoreVersion, new_web_library_detail.LastBundleManifestIgnoreVersion, new_web_library_detail.Id + "::" + nameof(old.LastBundleManifestIgnoreVersion), ref state);
                    old.LastSynced = MixOldAndNew(old.LastSynced, new_web_library_detail.LastSynced, new_web_library_detail.Id + "::" + nameof(old.LastSynced), ref state);
                    old.IntranetPath = MixOldAndNew(old.IntranetPath, new_web_library_detail.IntranetPath, new_web_library_detail.Id + "::" + nameof(old.IntranetPath), ref state, PathsMixCollisionComparer);
                    old.FolderToWatch = MixOldAndNew(old.FolderToWatch, new_web_library_detail.FolderToWatch, new_web_library_detail.Id + "::" + nameof(old.FolderToWatch), ref state, PathsMixCollisionComparer);
                    old.Title = MixOldAndNew(old.Title, new_web_library_detail.Title, new_web_library_detail.Id + "::" + nameof(old.Title), ref state);
                    old.Description = MixOldAndNew(old.Description, new_web_library_detail.Description, new_web_library_detail.Id + "::" + nameof(old.Description), ref state);
                    old.BundleManifestJSON = MixOldAndNew(old.BundleManifestJSON, new_web_library_detail.BundleManifestJSON, new_web_library_detail.Id + "::" + nameof(old.BundleManifestJSON), ref state);
                    /* old.DescriptiveTitle = */
                    //MixOldAndNew(old.DescriptiveTitle, new_web_library_detail.DescriptiveTitle, new_web_library_detail.Id + "::" + nameof(old.DescriptiveTitle), ref state);
                    MixOldAndNew(old.LibraryType(), new_web_library_detail.LibraryType(), new_web_library_detail.Id + "::" + nameof(old.LibraryType), ref state);
                    old.Deleted = MixOldAndNew(old.Deleted, new_web_library_detail.Deleted, new_web_library_detail.Id + "::" + nameof(old.Deleted), ref state);
                    old.AutoSync = MixOldAndNew(old.AutoSync, new_web_library_detail.AutoSync, new_web_library_detail.Id + "::" + nameof(old.AutoSync), ref state);
                    /* old.IsBundleLibrary = */
                    MixOldAndNew(old.IsBundleLibrary, new_web_library_detail.IsBundleLibrary, new_web_library_detail.Id + "::" + nameof(old.IsBundleLibrary), ref state);
                    /* old.IsIntranetLibrary = */
                    MixOldAndNew(old.IsIntranetLibrary, new_web_library_detail.IsIntranetLibrary, new_web_library_detail.Id + "::" + nameof(old.IsIntranetLibrary), ref state);
                    /* old.IsWebLibrary = */

                    ASSERT.Test(old.Xlibrary != null ? new_web_library_detail.Xlibrary == null : true);
#if false    // this code is not needed in the current use of the API as long as the ASSERTions above hold
                    old.library?.Dispose();
                    if (new_web_library_detail.library != null)
                    {
                        old.library = new_web_library_detail.library;
                    }
                    // and update it's internal (cyclic) web_library_detail reference:
                    if (old.library != null)
                    {
                        old.library.WebLibraryDetail = old;
                    }
#endif

                    if ((state & 0x0c) == 0x0c)
                    {
                        Logging.Warn("library info has been mixed as part of collision resolution for library {0}.", old.Id);
                    }
                    else if ((state & 0x04) == 0x04)
                    {
                        Logging.Info("library info has been kept as-is as part of collision resolution for library {0}.", old.Id);
                    }
                    if ((state & 0x08) == 0x08)
                    {
                        Logging.Warn("library info has been picked up from the new entry as part of collision resolution for library {0}.", old.Id);
                    }

                    new_web_library_detail = old;
                }
            }

            //ASSERT.Test(new_web_library_detail.Xlibrary == null);
            web_library_details[new_web_library_detail.Id] = new_web_library_detail;

            if (!suppress_flush_to_disk)
            {
                SaveKnownWebLibraries();

                StatusManager.Instance.ClearStatus("LibraryInitialLoad");

                FireWebLibrariesChanged();
            }
        }

        public void UpdateKnownWebLibraryFromIntranet(string intranet_path, bool suppress_flush_to_disk = true, string extra_info_message_on_skip = "")
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            Logging.Info("+Updating known Intranet Library from {0}", intranet_path);

            IntranetLibraryDetail intranet_library_detail = IntranetLibraryDetail.Read(IntranetLibraryTools.GetLibraryDetailPath(intranet_path));

            WebLibraryDetail new_web_library_detail = new WebLibraryDetail();
            new_web_library_detail.IntranetPath = intranet_path;
            new_web_library_detail.Id = intranet_library_detail.Id;
            new_web_library_detail.Title = intranet_library_detail.Title;
            new_web_library_detail.Description = intranet_library_detail.Description;
            new_web_library_detail.Deleted = false;

            UpdateKnownWebLibrary(new_web_library_detail, suppress_flush_to_disk, extra_info_message_on_skip);

            Logging.Info("-Updating known Intranet Library from {0}", intranet_path);
        }

        public WebLibraryDetail UpdateKnownWebLibraryFromBundleLibraryManifest(BundleLibraryManifest manifest, bool suppress_flush_to_disk = true)
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            Logging.Info("+Updating known Bundle Library {0} ({1})", manifest.Title, manifest.Id);

            WebLibraryDetail new_web_library_detail = new WebLibraryDetail();
            new_web_library_detail.BundleManifestJSON = manifest.ToJSON();
            //new_web_library_detail.IsBundleLibrary = true;
            new_web_library_detail.Id = manifest.Id;
            new_web_library_detail.Title = manifest.Title;
            new_web_library_detail.Description = manifest.Description;
            new_web_library_detail.Deleted = false;

            UpdateKnownWebLibrary(new_web_library_detail, suppress_flush_to_disk);

            Logging.Info("-Updating known Bundle Library {0} ({1})", manifest.Title, manifest.Id);

            return new_web_library_detail;
        }

        internal void ForgetKnownWebLibraryFromIntranet(WebLibraryDetail web_library_detail)
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            Logging.Info("+Forgetting {1} Library from {0}", web_library_detail.Title, web_library_detail.LibraryType());

            if (MessageBoxes.AskQuestion("Are you sure you want to forget the {1} Library '{0}'?", web_library_detail.Title, web_library_detail.LibraryType()))
            {
                web_library_details.Remove(web_library_detail.Id);

                SaveKnownWebLibraries();
                FireWebLibrariesChanged();
            }

            Logging.Info("-Forgetting {1} Library from {0}", web_library_detail.Title, web_library_detail.LibraryType());
        }

        #endregion
    }
}
