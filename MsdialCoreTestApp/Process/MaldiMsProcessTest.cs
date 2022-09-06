﻿using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Parameter;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialImmsCore.DataObj;
using CompMs.MsdialImmsCore.Parameter;
using CompMs.MsdialIntegrate.Parser;
using CompMs.RawDataHandler.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CompMs.App.MsdialConsole.Process
{
    public sealed class MaldiMsProcessTest {
        private MaldiMsProcessTest() { }
        public static void TimsOnTest() {
            var filepath = @"D:\msdial_test\test_data\Brain-C-3-9AA-ON\Brain-C-3-9AA-TIMS-ON-1.d";
            var reffile = @"D:\msdial_test\test_data\imaging\20220725_timsTOFpro_TextLibrary_Brain_Neg.txt";
            var outputfile = @"D:\msdial_test\test_data\Brain-C-3-9AA-ON\Brain-C-3-9AA-TIMS-ON-1_msdial.mddata";
            var filename = Path.GetFileNameWithoutExtension(filepath);
            var fileDir = Path.GetDirectoryName(filepath);
            var projectParameter = new ProjectParameter(DateTime.Now, @"D:\msdial_test\test_data\Brain-C-3-9AA-ON\", "Brain-C-3-9AA-TIMS-ON-1.mdproject");
            var storage = new ProjectDataStorage(projectParameter);
            var file = new AnalysisFileBean() {
                AnalysisFileId = 0,
                AnalysisFileIncluded = true,
                AnalysisFileName = filename,
                AnalysisFilePath = filepath,
                AnalysisFileAnalyticalOrder = 1,
                AnalysisFileClass = "0",
                AnalysisFileType = AnalysisFileType.Sample,
                DeconvolutionFilePath = Path.Combine(fileDir, filename + "_test220906" + ".dcl"),
                PeakAreaBeanInformationFilePath = Path.Combine(fileDir, filename + "_test220906" + ".pai"),
            };
            var param = new MsdialImmsParameter() {
                ProjectFolderPath = Path.GetDirectoryName(outputfile),
                ProjectFileName = Path.GetFileName(outputfile),
                MachineCategory = MachineCategory.IIMMS,
                TextDBFilePath = reffile,
                IonMode = IonMode.Negative,
                MinimumAmplitude = 10000,
                FileID2CcsCoefficients = new Dictionary<int, CoefficientsForCcsCalculation>() {
                    { 0, new CoefficientsForCcsCalculation() { IsBrukerIM = true } }
                }
            };
            CommonProcess.ParseLibraries(param, -1, 
                out var iupacDB, 
                out var mspDB, 
                out var txtDB, 
                out var isotopeTextDB, 
                out var compoundsInTargetMode);

            RawMeasurement rawobj = null;
            Console.WriteLine("Reading data...");
            using (var access = new RawDataAccess(filepath, 0, false, true, false)) {
                rawobj = access.GetMeasurement();
            }
            Console.WriteLine("Peak picking...");
            var provider = new StandardDataProviderFactory().Create(rawobj);
            var container = new MsdialImmsDataStorage {
                AnalysisFiles = new List<AnalysisFileBean>() { file }, 
                AlignmentFiles = new List<AlignmentFileBean>(),
                MspDB = mspDB, TextDB = txtDB, IsotopeTextDB = isotopeTextDB, IupacDatabase = iupacDB, MsdialImmsParameter = param
            };
            var database = new MoleculeDataBase(txtDB, reffile, DataBaseSource.Text, SourceType.TextDB);
            var annotator = new MsdialImmsCore.Algorithm.Annotation.ImmsTextDBAnnotator(database, param.TextDbSearchParam, param.TextDBFilePath, 1);
            container.DataBases = DataBaseStorage.CreateEmpty();
            container.DataBases.AddMoleculeDataBase(
                database,
                new List<IAnnotatorParameterPair<IAnnotationQuery, Common.Components.MoleculeMsReference, MsScanMatchResult, MoleculeDataBase>> { 
                    new MetabolomicsAnnotatorParameterPair(annotator, param.TextDbSearchParam)
                }
            );
            storage.AddStorage(container);

            var evaluator = MsScanMatchResultEvaluator.CreateEvaluator(param.TextDbSearchParam);
            MsdialImmsCore.Process.FileProcess.Run(file, container, null, null, provider, evaluator, false, null);
            using (var fs = File.Open(storage.ProjectParameter.FilePath, FileMode.Create))
            using (var streamManager = ZipStreamManager.OpenCreate(fs)) {
                var serializer = new MsdialIntegrateSerializer();
                storage.Save(
                    streamManager,
                    serializer,
                    path => new DirectoryTreeStreamManager(path),
                    parameter => { }).Wait();
            }

            var features = MsdialPeakSerializer.LoadChromatogramPeakFeatures(file.PeakAreaBeanInformationFilePath);
            RawSpectraOnPixels pixelData = null;
            var featureElements = features.Select(n => new Raw2DElement(n.Mass, n.ChromXsTop.Value)).ToList();
            Console.WriteLine("Reading data...");
            using (var access = new RawDataAccess(filepath, 0, false, true, false)) {
                pixelData = access.GetRawPixelFeatures(featureElements, null);
            }

            foreach (var (feature, pixel) in IEnumerableExtension.Zip(features, pixelData.PixelPeakFeaturesList)) {
                
                if (feature.IsReferenceMatched(evaluator)) {
                    Console.WriteLine("Name\t" + feature.Name);
                    Console.WriteLine("MZ\t" + feature.PrecursorMz);
                    Console.WriteLine("Drift\t" + feature.ChromXsTop.Value);
                    Console.WriteLine("CCS\t" + feature.CollisionCrossSection);

                    var refdata = container.TextDB[feature.MatchResults.Representative.LibraryID];
                    Console.WriteLine("RefMz\t" + refdata.PrecursorMz);
                    Console.WriteLine("RefCCS\t" + refdata.CollisionCrossSection);

                    Console.WriteLine("PixelCount\t" + pixel.IntensityArray.Length);
                    Console.WriteLine("X_Index\tY_Index\tX_UM\tY_UM\tIntensity");
                    foreach (var (intensity, frame) in pixel.IntensityArray.Zip(pixelData.XYFrames, (intensity, frame) => (intensity, frame))) {
                        var x_index = frame.XIndexPos;
                        var y_index = frame.YIndexPos;
                        var x_um = frame.MotorPositionX;
                        var y_um = frame.MotorPositionY;

                        Console.WriteLine(x_index + "\t" + y_index + "\t" + x_um + "\t" + y_um + "\t" + intensity);
                    }
                }
            }
        }


    }

    

}
