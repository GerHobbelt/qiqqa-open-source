﻿using System;
using System.Diagnostics;
using System.Text;

namespace Utilities.ProcessTools
{
    public class ProcessSpawning
    {
        /// <summary>
        /// Sets the working directory to that where the application launched
        /// </summary>
        /// <param name="executable_filename"></param>
        /// <param name="process_parameters"></param>
        /// <param name="priority_class"></param>
        /// <returns></returns>
        public static Process SpawnChildProcess(string executable_filename, string process_parameters = null, ProcessPriorityClass priority_class = ProcessPriorityClass.BelowNormal, bool stdout_is_binary = false)
        {
            ProcessStartInfo process_start_info = new ProcessStartInfo();
            process_start_info.RedirectStandardError = true;
            process_start_info.RedirectStandardInput = true;
            process_start_info.RedirectStandardOutput = true;
            process_start_info.UseShellExecute = false;
            process_start_info.WorkingDirectory = UnitTestDetector.StartupDirectoryForQiqqa; // Application.StartupPath;
            process_start_info.FileName = executable_filename;
            process_start_info.CreateNoWindow = true;
            process_start_info.Arguments = process_parameters;
            process_start_info.StandardErrorEncoding = Encoding.UTF8;
            if (!stdout_is_binary)
            {
                process_start_info.StandardOutputEncoding = Encoding.UTF8;
            }

            Process process = new Process();
            process.StartInfo = process_start_info;
            process.Start();

            try
            {
                process.PriorityClass = priority_class;
            }
            catch (Exception ex)
            {
                Logging.Warn(ex, "There was a problem setting the process priority class for {0} (it has probably already exited)", executable_filename);
            }

            return process;
        }
    }
}
