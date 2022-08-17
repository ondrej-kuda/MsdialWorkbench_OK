﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Setting;
using CompMs.Common.Enum;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Core
{

    internal abstract class MethodModelBase : BindableBase, IMethodModel, IDisposable
    {
        public MethodModelBase(
            IEnumerable<AnalysisFileBean> analysisFiles,
            IEnumerable<AlignmentFileBean> alignmentFiles,
            ProjectBaseParameterModel projectBaseParameter) {
            if (projectBaseParameter is null) {
                throw new ArgumentNullException(nameof(projectBaseParameter));
            }

            AnalysisFiles = new ObservableCollection<AnalysisFileBean>(analysisFiles ?? new AnalysisFileBean[] { });
            AlignmentFiles = new ObservableCollection<AlignmentFileBean>(alignmentFiles ?? new AlignmentFileBean[] { });

            FilePropertySetModel = new AnalysisFilePropertySetModel(AnalysisFiles, projectBaseParameter);
        }

        public AnalysisFileBean AnalysisFile {
            get => analysisFile;
            set => SetProperty(ref analysisFile, value);
        }
        private AnalysisFileBean analysisFile;

        public ObservableCollection<AnalysisFileBean> AnalysisFiles { get; }

        public IAnalysisModel AnalysisModelBase {
            get => analysisModelBase;
            private set => SetProperty(ref analysisModelBase, value);
        }
        private IAnalysisModel analysisModelBase;

        public Task LoadAnalysisFileAsync(AnalysisFileBean analysisFile, CancellationToken token) {
            if (AnalysisFile == analysisFile || analysisFile is null) {
                return Task.CompletedTask;
            }
            var task = AnalysisModelBase?.SaveAsync(token) ?? Task.CompletedTask;
            AnalysisFile = analysisFile;
            AnalysisModelBase = LoadAnalysisFileCore(AnalysisFile);

            return task;
        }

        protected abstract IAnalysisModel LoadAnalysisFileCore(AnalysisFileBean analysisFile);

        public AlignmentFileBean AlignmentFile {
            get => alignmentFile;
            set => SetProperty(ref alignmentFile, value);
        }
        private AlignmentFileBean alignmentFile;

        public ObservableCollection<AlignmentFileBean> AlignmentFiles { get; }
        public IAlignmentModel AlignmentModelBase {
            get => alignmentModelBase;
            private set => SetProperty(ref alignmentModelBase, value);
        }
        private IAlignmentModel alignmentModelBase;

        public Task LoadAlignmentFileAsync(AlignmentFileBean alignmentFile, CancellationToken token) {
            if (AlignmentFile == alignmentFile || alignmentFile is null) {
                return Task.CompletedTask;
            }
            var task = AlignmentModelBase?.SaveAsync() ?? Task.CompletedTask;

            AlignmentFile = alignmentFile;
            AlignmentModelBase = LoadAlignmentFileCore(AlignmentFile);

            return task;
        }

        protected abstract IAlignmentModel LoadAlignmentFileCore(AlignmentFileBean alignmentFile);

        public abstract Task RunAsync(ProcessOption option, CancellationToken token);

        public AnalysisFilePropertySetModel FilePropertySetModel { get; }

        public virtual Task SaveAsync() {
            return Task.WhenAll(new List<Task>
            {
                AnalysisModelBase?.SaveAsync(default) ?? Task.CompletedTask,
                AlignmentModelBase?.SaveAsync() ?? Task.CompletedTask,
            });
        }

        private bool disposedValue;
        protected CompositeDisposable Disposables = new CompositeDisposable();

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Disposables.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public int GetNumberOfThreadToBeUsed(int userDefinedNumThread) {
            var numThreads = 1;
            if (userDefinedNumThread > 1) {
                // for advanced settings; ignore max ProcessorCount
                var lp = Environment.ProcessorCount;
                if (userDefinedNumThread > lp + 1) {
                    userDefinedNumThread = lp;
                }
                numThreads = userDefinedNumThread;
            }
            return numThreads;
        }
    }
}