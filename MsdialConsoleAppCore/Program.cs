﻿using Msdial.Lcms.Dataprocess.Algorithm.Clustering;
using Riken.Metabolomics.MsdialConsoleApp.Process;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Msdial.Lcms.Dataprocess.Test;
using CompMs.RawDataHandler.Core;

namespace Riken.Metabolomics.MsdialConsoleApp
{
    public class Program
    {
        /// <summary>
        /// [0] should be analysis type (gcms or lcms-dia or lcms-dda)
        /// -i: import folder
        /// -m: method file
        /// -o: output folder (deconvolution/alignment results)
        /// -p: option, call it if you want to genarate MTD file to be used in MSDIAL GUI application.
        /// </summary>
        /// <param name="args"></param>
        public static int Main(string[] args) {

            #region arg[] examples
            //GC - MS test using netCDF
            //args = new string[] {
            //  "gcms"
            //  , "-i"
            //  , @"\\mtbdt\Mtb_info\data\MS-DIAL_aono_190828\RawDataset(console)"
            //  , "-m"
            //  , @"\\mtbdt\Mtb_info\data\MS-DIAL_aono_190828\Msdial-GCMS-Param.txt"
            //  , "-o"
            //  , @"\\mtbdt\Mtb_info\data\MS-DIAL_aono_190828\RawDataset(console)\result"
            //  , "-p"
            //};

            //LC-DDA test using abf (as centroid mode)
            //args = new string[] {
            //    "lcmsdda"
            //    , "-i"
            //    , @"D:\Lecture for metabolomics software\LCMSMS Wine raw data"
            //    , "-o"
            //    , @"D:\Lecture for metabolomics software\LCMSMS Wine raw data"
            //    , "-m"
            //    , @"D:\Lecture for metabolomics software\LCMSMS Wine raw data\Msdial-lcms-dda-Param20200527.txt"
            //    , "-p" };

            //LC-DDA test using mzML(as profile mode)
            //args = new string[] {
            //    "lcmsdda"
            //    , "-i"
            //    , @"D:\Msdial-ConsoleApp-Demo files\Msdial-ConsoleApp-Demo files for DDA\ABF"
            //    , "-m"
            //    , @"D:\Msdial-ConsoleApp-Demo files\Msdial-ConsoleApp-Demo files for DDA\Msdial-lcms-dda-Param.txt"
            //    , "-o"
            //    , @"D:\Msdial-ConsoleApp-Demo files\Msdial-ConsoleApp-Demo files for DDA\ABF" };

            //LC-DIA(SWATH) test using mzML (as profile mode)
            //args = new string[] {
            //    "lcmsdia" 
            //    , "-i"
            //    , @"D:\Msdial-ConsoleApp-Demo files\Msdial-ConsoleApp-Demo files for DIA\MzML"
            //    , "-m"
            //    , @"D:\Msdial-ConsoleApp-Demo files\Msdial-ConsoleApp-Demo files for DIA\Msdial-lcms-dia-Param.txt"
            //    , "-o"
            //    , @"D:\Msdial-ConsoleApp-Demo files\Msdial-ConsoleApp-Demo files for DIA\MzML" };

            //LC-DIA(SWATH) test using ABF (as profile mode)
            //args = new string[] {
            //    "lcmsdia"
            //    , "-i"
            //    , @"C:\Users\Hiroshi Tsugawa\Desktop\MsdialConsoleApp demo files\LCMS_DIA"
            //    , "-m"
            //    , @"C:\Users\Hiroshi Tsugawa\Desktop\MsdialConsoleApp demo files\LCMS_DIA\Msdial-lcms-dia-Param.txt"
            //    , "-o"
            //    , @"C:\Users\Hiroshi Tsugawa\Desktop\MsdialConsoleApp demo files\LCMS_DIA" };
            #endregion

            /*
            args = new string[] {

            "lcmsdia",
                "-i",
                @"D:\1_study\others\acetylcarnosine_chemstd",
                "-m",
                @"D:\1_study\others\setting_files\MS-DIAL-LCMS-AIF-Parameters_V01.txt",
                "-o",
                @"D:\1_study\others\acetylcarnosine_chemstd",
                "-p", "-mCE" ,"-target", "269.117"
            };           
            */

            return MainProcess.Run(args);
        }
    }
}
