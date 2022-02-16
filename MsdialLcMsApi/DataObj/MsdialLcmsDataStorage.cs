﻿using CompMs.Common.MessagePack;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialLcmsApi.Parameter;
using CompMs.MsdialLcMsApi.Parser;
using MessagePack;
using System.IO;
using System.Threading.Tasks;

namespace CompMs.MsdialLcMsApi.DataObj
{
    [MessagePackObject]
    public sealed class MsdialLcmsDataStorage : MsdialDataStorageBase, IMsdialDataStorage<MsdialLcmsParameter> {
        [Key(6)]
        public MsdialLcmsParameter MsdialLcmsParameter { get; set; }

        MsdialLcmsParameter IMsdialDataStorage<MsdialLcmsParameter>.Parameter => MsdialLcmsParameter;

        protected override void SaveMsdialDataStorageCore(Stream stream) {
            MessagePackDefaultHandler.SaveToStream(this, stream);
        }

        protected override void SaveDataBaseMapper(Stream stream) {

        }

        public static IMsdialSerializer Serializer { get; } = new MsdialLcmsSerializer();

        class MsdialLcmsSerializer : MsdialSerializer, IMsdialSerializer
        {
            protected override async Task<IMsdialDataStorage<ParameterBase>> LoadMsdialDataStorageCoreAsync(IStreamManager streamManager, string path) {
                using (var stream = await streamManager.Get(path).ConfigureAwait(false)) {
                    return MessagePackDefaultHandler.LoadFromStream<MsdialLcmsDataStorage>(stream);
                }
            }

            protected override async Task LoadDataBasesAsync(IStreamManager streamManager, string path, IMsdialDataStorage<ParameterBase> storage, string projectFolderPath) {
                using (var stream = await streamManager.Get(path).ConfigureAwait(false)) {
                    storage.DataBases = DataBaseStorage.Load(stream, new LcmsLoadAnnotatorVisitor(storage.Parameter), projectFolderPath);
                }
            }
        }
    }
}
