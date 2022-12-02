﻿using CompMs.CommonMVVM;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Setting
{
    internal sealed class ProcessSettingModel : DisposableModelBase
    {
        public ProcessSettingModel(ProjectSettingModel projectSettingModel, DatasetSettingModel datasetSettingModel = null, MethodSettingModel methodSettingModel = null) {
            ProjectSettingModel = projectSettingModel ?? throw new System.ArgumentNullException(nameof(projectSettingModel));
            if (datasetSettingModel is null) {
                DatasetSettingModel = projectSettingModel
                    .ObserveProperty(m => m.DatasetSettingModel)
                    .ToReadOnlyReactivePropertySlim()
                    .AddTo(Disposables);
            }
            else {
                DatasetSettingModel = new ReadOnlyReactivePropertySlim<DatasetSettingModel>(Observable.Return(datasetSettingModel)).AddTo(Disposables);
            }
            if (methodSettingModel is null) {
                MethodSettingModel = DatasetSettingModel
                    .Select(m => m?.ObserveProperty(m_ => m_.MethodSettingModel, isPushCurrentValueAtFirst: false).StartWith(m.MethodSettingModel) ?? Observable.Never<MethodSettingModel>())
                    .Switch()
                    .ToReadOnlyReactivePropertySlim()
                    .AddTo(Disposables);
            }
            else {
                MethodSettingModel = new ReadOnlyReactivePropertySlim<MethodSettingModel>(Observable.Return(methodSettingModel)).AddTo(Disposables);
            }
        }

        public ProjectSettingModel ProjectSettingModel { get; }

        public ReadOnlyReactivePropertySlim<DatasetSettingModel> DatasetSettingModel { get; }

        public ReadOnlyReactivePropertySlim<MethodSettingModel> MethodSettingModel { get; }

        public async Task RunProcessAsync() {
            if (ProjectSettingModel is null || DatasetSettingModel.Value is null || MethodSettingModel.Value is null) {
                return;
            }
            var runSuccess = await MethodSettingModel.Value.TryRunAsync(default);
            if (!runSuccess) {
                return;
            }
            DatasetSettingModel.Value.Run();
            var projectRunTask = ProjectSettingModel.RunAsync();
            await projectRunTask;
        }
    }
}
