﻿using System;
using System.Collections.Generic;
using System.Linq;
using Qiqqa.Common.Configuration;
using Utilities;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


namespace Qiqqa.Synchronisation.PDFSync
{
    /// <summary>
    /// This class keeps track of which PDFs the server has told us it already owns so that the client does not have to interrogate again whether or not to upload a specific PDF.
    /// </summary>
    internal class PDFsAlreadyStoredOnServerCache
    {
        public static readonly PDFsAlreadyStoredOnServerCache Instance = new PDFsAlreadyStoredOnServerCache();

        private bool is_dirty = false;
        private HashSet<string> tokens = new HashSet<string>();
        private object tokens_lock = new object();

        private PDFsAlreadyStoredOnServerCache()
        {
            Load();
        }


        private string FILENAME => Path.GetFullPath(Path.Combine(ConfigurationManager.Instance.BaseDirectoryForUser, @"Qiqqa.server_stored_pdfs_cache"));

        private void Load()
        {
            // Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (tokens_lock)
            {
                // l1_clk.LockPerfTimerStop();
                try
                {
                    if (File.Exists(FILENAME))
                    {
                        string[] lines = File.ReadAllLines(FILENAME);
                        foreach (var line in lines)
                        {
                            tokens.Add(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Warn(ex, "There was a problem loading {0}", FILENAME);
                }
            }
        }

        public void Save()
        {
            // Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (tokens_lock)
            {
                // l1_clk.LockPerfTimerStop();
                if (!is_dirty) return;
                try
                {
                    File.WriteAllLines(FILENAME, tokens.ToArray());
                }
                catch (Exception ex)
                {
                    Logging.Warn(ex, "There was a problem saving {0}", FILENAME);
                }
            }
        }


        public void Add(string token)
        {
            // Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (tokens_lock)
            {
                // l1_clk.LockPerfTimerStop();
                if (tokens.Add(token))
                {
                    is_dirty = true;
                }
            }
        }

        public bool IsAlreadyCached(string token)
        {
            // Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (tokens_lock)
            {
                // l1_clk.LockPerfTimerStop();
                return tokens.Contains(token);
            }
        }
    }
}
