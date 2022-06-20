﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.CommonMVVM;
using CompMs.Graphics.Core.Base;
using CompMs.Graphics.AxisManager.Generic;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;

namespace CompMs.App.Msdial.Model.Chart
{
    internal sealed class AnalysisPeakPlotModel : DisposableModelBase
    {
        public AnalysisPeakPlotModel(
            ObservableCollection<ChromatogramPeakFeatureModel> spots,
            Func<ChromatogramPeakFeatureModel, double> horizontalSelector,
            Func<ChromatogramPeakFeatureModel, double> verticalSelector,
            IReactiveProperty<ChromatogramPeakFeatureModel> targetSource,
            IObservable<string> labelSource,
            BrushMapData<ChromatogramPeakFeatureModel> selectedBrush,
            IList<BrushMapData<ChromatogramPeakFeatureModel>> brushes,
            IAxisManager<double> horizontalAxis = null,
            IAxisManager<double> verticalAxis = null) {
            if (brushes is null) {
                throw new ArgumentNullException(nameof(brushes));
            }

            Spots = spots ?? throw new ArgumentNullException(nameof(spots));
            HorizontalSelector = horizontalSelector ?? throw new ArgumentNullException(nameof(horizontalSelector));
            VerticalSelector = verticalSelector ?? throw new ArgumentNullException(nameof(verticalSelector));
            LabelSource = labelSource ?? throw new ArgumentNullException(nameof(labelSource));
            SelectedBrush = selectedBrush ?? throw new ArgumentNullException(nameof(selectedBrush));
            Brushes = new ReadOnlyCollection<BrushMapData<ChromatogramPeakFeatureModel>>(brushes);
            TargetSource = targetSource ?? throw new ArgumentNullException(nameof(targetSource));
            GraphTitle = string.Empty;
            HorizontalTitle = string.Empty;
            VerticalTitle = string.Empty;
            HorizontalProperty = string.Empty;
            VerticalProperty = string.Empty;

            HorizontalAxis = horizontalAxis ?? this.ObserveProperty(m => m.HorizontalRange)
                .ToReactiveContinuousAxisManager<double>(new RelativeMargin(0.05))
                .AddTo(Disposables);
            VerticalAxis = verticalAxis ?? this.ObserveProperty(m => m.VerticalRange)
                .ToReactiveContinuousAxisManager<double>(new RelativeMargin(0.05))
                .AddTo(Disposables);
        }

        public ObservableCollection<ChromatogramPeakFeatureModel> Spots { get; }

        public Range HorizontalRange {
            get {
                if (!Spots.Any() || HorizontalSelector == null) {
                    return new Range(0, 1);
                }
                var minimum = Spots.Min(HorizontalSelector);
                var maximum = Spots.Max(HorizontalSelector);
                return new Range(minimum, maximum);
            }
        }

        public Range VerticalRange {
            get {
                if (!Spots.Any() || VerticalSelector == null) {
                    return new Range(0, 1);
                }
                var minimum = Spots.Min(VerticalSelector);
                var maximum = Spots.Max(VerticalSelector);
                return new Range(minimum, maximum);
            }
        }

        public IAxisManager<double> HorizontalAxis { get; }

        public IAxisManager<double> VerticalAxis { get; }

        public IReactiveProperty<ChromatogramPeakFeatureModel> TargetSource { get; }

        public Func<ChromatogramPeakFeatureModel, double> HorizontalSelector {
            get => horizontalSelector;
            set {
                if (SetProperty(ref horizontalSelector, value)) {
                    OnPropertyChanged(nameof(HorizontalRange));
                }
            }
        }
        private Func<ChromatogramPeakFeatureModel, double> horizontalSelector;

        public Func<ChromatogramPeakFeatureModel, double> VerticalSelector {
            get => verticalSelector;
            set {
                if (SetProperty(ref verticalSelector, value)) {
                    OnPropertyChanged(nameof(VerticalRange));
                }
            }
        }
        private Func<ChromatogramPeakFeatureModel, double> verticalSelector;

        public string GraphTitle {
            get => graphTitle;
            set => SetProperty(ref graphTitle, value);
        }
        private string graphTitle;

        public string HorizontalTitle {
            get => horizontalTitle;
            set => SetProperty(ref horizontalTitle, value);
        }
        private string horizontalTitle;

        public string VerticalTitle {
            get => verticalTitle;
            set => SetProperty(ref verticalTitle, value);
        }
        private string verticalTitle;

        public string HorizontalProperty {
            get => horizontalProperty;
            set => SetProperty(ref horizontalProperty, value);
        }
        private string horizontalProperty;

        public string VerticalProperty {
            get => verticalProperty;
            set => SetProperty(ref verticalProperty, value);
        }
        private string verticalProperty;

        public IObservable<string> LabelSource { get; }
        public BrushMapData<ChromatogramPeakFeatureModel> SelectedBrush {
            get => _selectedBrush;
            set => SetProperty(ref _selectedBrush, value);
        }
        private BrushMapData<ChromatogramPeakFeatureModel> _selectedBrush;

        public ReadOnlyCollection<BrushMapData<ChromatogramPeakFeatureModel>> Brushes { get; }
    }
}
