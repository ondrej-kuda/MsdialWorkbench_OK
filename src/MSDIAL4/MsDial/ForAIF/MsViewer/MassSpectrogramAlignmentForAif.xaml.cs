﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Msdial.Lcms.Dataprocess.Algorithm;

namespace Rfx.Riken.OsakaUniv.MsViewer
{
    /// <summary>
    /// MassSpectrogramAlignmentForAif.xaml の相互作用ロジック
    /// </summary>
    public partial class MassSpectrogramAlignmentForAif : Window
    {

        public MassSpectrogramAlignmentForAifVM MassSpectrogramAlignmentForAifVM { get; set; }

        public MassSpectrogramAlignmentForAif() {
            InitializeComponent();
        }

        public MassSpectrogramAlignmentForAif(AifViewControlCommonProperties commonProp, AlignmentResultBean AlignmentResultBean, List<MS2DecResult> mS2DecResList, List<string> nameList, int peakId) {
            InitializeComponent();
            MassSpectrogramAlignmentForAifVM = new MassSpectrogramAlignmentForAifVM(this, commonProp, AlignmentResultBean, mS2DecResList, peakId);

            initialize(commonProp, mS2DecResList, nameList);
        }



        private void initialize(AifViewControlCommonProperties commonProp, List<MS2DecResult> decResList, List<string> nameList) {
            this.Width = commonProp.NumDec * 400;

            for (var i = 0; i < commonProp.NumDec; i++) {
                this.MainViewer.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });
                var newGrid = new Grid() {
                    HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetColumn(newGrid, i);

                var msUC = new MassSpectrogramAlignmentUC(commonProp, decResList[i], MassSpectrogramAlignmentForAifVM.AlignmentProperty, nameList[i], MassSpectrogramAlignmentForAifVM.ReferenceMassSpectrogram[i], i);
                msUC.MassSpectrogramAlignmentVM.SetScores(MassSpectrogramAlignmentForAifVM.CompName[i], MassSpectrogramAlignmentForAifVM.ScoresList[i]);
                newGrid.Children.Add(msUC);
                this.MainViewer.Children.Add(newGrid);
                
                if (i > 0) {
                    var newGridSp = new GridSplitter() { Width = 3, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.Gray};
                    Grid.SetColumn(newGridSp, i);
                    Grid.SetRowSpan(newGridSp, 1);
                    this.MainViewer.Children.Add(newGridSp);
                }
            }
        }

        public void Refresh(List<MS2DecResult> decResList, int peakId) {
            this.MassSpectrogramAlignmentForAifVM.Refresh(decResList, peakId);
        }

        public void RefreshReference(MspFormatCompoundInformationBean msp) {
            this.MassSpectrogramAlignmentForAifVM.RefreshReference(msp);
        }



        public void FileChange(AifViewControlCommonProperties commonProp, AlignmentResultBean alignmentRes, List<MS2DecResult> decResList, int peakId) {
            this.MassSpectrogramAlignmentForAifVM.FileChange(commonProp, alignmentRes, decResList, peakId);
        }
    }

    public class MassSpectrogramAlignmentForAifVM
    {
        public List<MassSpectrogramBean> ReferenceMassSpectrogram { get; set; }
        public List<string> CompName { get; set; }
        public List<List<float>> ScoresList { get; set; }
        public AifViewControlCommonProperties CommonProp { get; set; }
        private MassSpectrogramAlignmentForAif massSpectrogramAlignmentForAif;
        private int peakID;
        public  AlignmentPropertyBean AlignmentProperty { get; set; }
        public AlignmentResultBean AlignmentResult { get; set; }
        private List<MS2DecResult> mS2DecResList { get; set; }

        public MassSpectrogramAlignmentForAifVM(MassSpectrogramAlignmentForAif massSpectrogramAlignmentForAif, AifViewControlCommonProperties commonProp, AlignmentResultBean AlignmentResultBean, List<MS2DecResult> mS2DecResList, int peakId) {
            this.massSpectrogramAlignmentForAif = massSpectrogramAlignmentForAif;
            this.CommonProp = commonProp;
            this.peakID = peakId;
            this.AlignmentResult = AlignmentResultBean;
            this.AlignmentProperty = AlignmentResult.AlignmentPropertyBeanCollection[this.peakID];

            this.mS2DecResList = mS2DecResList;
            setReferenceInformation();
            //Console.WriteLine("working");
            //foreach(var id in this.AlignmentProperty.LibraryIdList)
            //{
            //    Console.WriteLine("alignment library id : " + id);
            //}
        }

        public void FileChange(AifViewControlCommonProperties commonProp, AlignmentResultBean AlignmentResultBean, List<MS2DecResult> decResList, int peakId) {
            this.CommonProp = commonProp;
            this.peakID = peakId;
            this.mS2DecResList = decResList;
            this.AlignmentResult = AlignmentResultBean;
            this.AlignmentProperty = AlignmentResult.AlignmentPropertyBeanCollection[this.peakID];
            setReferenceInformation();
            for (var j = 0; j < (CommonProp.NumDec * 2 - 1); j++) {
                var i = j > 0 ? (j + 1) / 2 : 0;
                var grid2 = (Grid)VisualTreeHelper.GetChild(this.massSpectrogramAlignmentForAif.MainViewer, j);
                var msUC2 = (MassSpectrogramAlignmentUC)VisualTreeHelper.GetChild(grid2, 0);
                msUC2.MassSpectrogramAlignmentVM.FileChange(CommonProp, this.mS2DecResList[i], ReferenceMassSpectrogram[i], this.AlignmentProperty, CompName[i], ScoresList[i]);
                if (j > 0) j++;
            }

        }


        public void Refresh(List<MS2DecResult> decResList, int peakId) {
            this.peakID = peakId;
            this.mS2DecResList = decResList;
            this.AlignmentProperty = AlignmentResult.AlignmentPropertyBeanCollection[this.peakID];
            setReferenceInformation();
            Refresh();
            Refresh();
        }

        public void Refresh() {
            for (var j = 0; j < (CommonProp.NumDec * 2 - 1); j++) {
                var i = j > 0 ? (j + 1) / 2 : 0;
                var grid = (Grid)VisualTreeHelper.GetChild(this.massSpectrogramAlignmentForAif.MainViewer, j);
                var msUC = (MassSpectrogramAlignmentUC)VisualTreeHelper.GetChild(grid, 0);
                msUC.MassSpectrogramAlignmentVM.Refresh(this.mS2DecResList[i], ReferenceMassSpectrogram[i], this.AlignmentProperty, CompName[i], ScoresList[i]);
                if (j > 0) j++;
            }
        }

        public void RefreshReference(MspFormatCompoundInformationBean msp) {
            ScoresList = new List<List<float>>();
            MassSpectrogramBean referenceMassSpectrogram = UtilityForAIF.GetReferenceSpectra(msp, Brushes.Red);
            for (var i = 0; i < CommonProp.NumDec; i++) {
                var ms2peaks = new ObservableCollection<double[]>(this.mS2DecResList[i].MassSpectra);
                ScoresList.Add(UtilityForAIF.GetScores(CommonProp.Param, msp, ms2peaks, this.AlignmentProperty.AlignedPeakPropertyBeanCollection[this.AlignmentProperty.RepresentativeFileID].AccurateMass, this.AlignmentProperty.AlignedPeakPropertyBeanCollection[this.AlignmentProperty.RepresentativeFileID].RetentionTime));
            }
            for (var j = 0; j < (CommonProp.NumDec * 2 - 1); j++) {
                var i = j > 0 ? (j + 1) / 2 : 0;
                var grid = (Grid)VisualTreeHelper.GetChild(this.massSpectrogramAlignmentForAif.MainViewer, j);
                var msUC = (MassSpectrogramAlignmentUC)VisualTreeHelper.GetChild(grid, 0);
                msUC.MassSpectrogramAlignmentVM.Refresh(referenceMassSpectrogram, msp.Name, ScoresList[i]);
                if (j > 0) j++;
            }

        }

        private void setReferenceInformation() {
            CompName = new List<string>();
            ScoresList = new List<List<float>>();
            ReferenceMassSpectrogram = new List<MassSpectrogramBean>();
            for (var i = 0; i < CommonProp.NumDec; i++)
            {
                var libId = -1;
                if (this.AlignmentProperty.LibraryIdList != null && this.AlignmentProperty.LibraryIdList.Count == CommonProp.NumDec)
                    libId = this.AlignmentProperty.LibraryIdList[i];
                else
                    libId = this.AlignmentProperty.LibraryID;

                if (CommonProp.MspDB == null || CommonProp.MspDB.Count == 0 || libId < 0 || libId > CommonProp.MspDB.Count -1) {
                    UtilityForAIF.SetNoReferense(CompName, ScoresList, ReferenceMassSpectrogram);
                    continue ;
                }
                var msp = CommonProp.MspDB[libId];
                CompName.Add(msp.Name);
                ReferenceMassSpectrogram.Add(UtilityForAIF.GetReferenceSpectra(msp, Brushes.Red));

                var ms2peaks = new ObservableCollection<double[]>(this.mS2DecResList[i].MassSpectra);
                ScoresList.Add(UtilityForAIF.GetScores(CommonProp.Param, msp, ms2peaks, this.AlignmentProperty.AlignedPeakPropertyBeanCollection[this.AlignmentProperty.RepresentativeFileID].AccurateMass, this.AlignmentProperty.AlignedPeakPropertyBeanCollection[this.AlignmentProperty.RepresentativeFileID].RetentionTime));
            }
        }

    }
}
