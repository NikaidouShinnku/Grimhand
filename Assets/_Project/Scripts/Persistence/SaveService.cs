using System;
using UnityEngine;

namespace Grimhand.Persistence
{
    public sealed class SaveService
    {
        readonly ISaveStorage _storage;
        readonly SaveValidationContext _validationContext;

        public SaveService(ISaveStorage storage, SaveValidationContext validationContext)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
        }

        public ISaveStorage Storage => _storage;

        public static string DefaultSaveDirectory
        {
            get
            {
                // 编辑器与正式包分开存，避免开发时完成教程后 Build 也跳过新手引导。
#if UNITY_EDITOR
                return System.IO.Path.Combine(Application.persistentDataPath, "saves_editor");
#else
                return System.IO.Path.Combine(Application.persistentDataPath, "saves");
#endif
            }
        }

        public SaveLoadResult LoadOrCreate(Func<PlayerProfileState> createNew)
        {
            if (createNew == null)
                throw new ArgumentNullException(nameof(createNew));

            if (!_storage.ExistsPrimary() && !TryReadBackupOnly(out _))
                return SaveLoadResult.New(createNew());

            if (TryLoadValidated(primary: true, out var profile, out var primaryError))
                return SaveLoadResult.FromPrimary(profile);

            if (TryLoadValidated(primary: false, out profile, out _))
                return SaveLoadResult.FromBackup(profile);

            if (TryReadPrimaryOnly(out var corruptJson))
                _storage.ArchiveCorrupt(corruptJson, primaryError ?? "invalid");

            return SaveLoadResult.FallbackNew(
                createNew(),
                string.IsNullOrEmpty(primaryError)
                    ? "读档失败，已创建新局。"
                    : primaryError);
        }

        public bool TrySave(PlayerProfileState profile, out string error)
        {
            error = "";
            if (profile == null)
            {
                error = "Profile 为空。";
                return false;
            }

            try
            {
                var dto = SaveDataMapper.ToDto(profile);
                if (!SaveValidator.TryValidate(dto, _validationContext, out error))
                    return false;

                SaveIntegrity.ApplyHash(dto);
                var json = JsonUtility.ToJson(dto, prettyPrint: true);
                if (!_storage.TryWrite(json, out error))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        bool TryLoadValidated(bool primary, out PlayerProfileState profile, out string error)
        {
            profile = null;
            error = "";
            var json = "";
            var ok = primary ? _storage.TryReadPrimary(out json) : _storage.TryReadBackup(out json);
            if (!ok)
            {
                error = primary ? "主档不存在或为空。" : "备份不存在或为空。";
                return false;
            }

            return TryParseProfile(json, out profile, out error);
        }

        bool TryParseProfile(string json, out PlayerProfileState profile, out string error)
        {
            profile = null;
            error = "";
            try
            {
                var dto = JsonUtility.FromJson<PlayerProfileSaveData>(json);
                if (dto == null)
                {
                    error = "JSON 解析失败。";
                    return false;
                }

                if (!SaveIntegrity.Verify(dto))
                {
                    error = "integrityHash 校验失败。";
                    return false;
                }

                if (!SaveValidator.TryValidate(dto, _validationContext, out error))
                    return false;

                profile = SaveDataMapper.FromDto(dto);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        bool TryReadPrimaryOnly(out string json) => _storage.TryReadPrimary(out json);

        bool TryReadBackupOnly(out string json) => _storage.TryReadBackup(out json);
    }
}
