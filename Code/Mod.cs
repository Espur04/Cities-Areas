using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Areas;
using Game.Common;
using Game.Input;
using Game.Modding;
using Game.Prefabs;
using Game.Rendering;
using Game.SceneFlow;
using Game.Settings;
using Game.Tools;
using Game.UI.InGame;
using System.IO;

namespace Communes
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"CitiesAreas.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Setting m_Setting;
        public static ProxyAction m_ToggleToolAction;
        public static Setting CurrentSetting { get; private set; }
        public const string kVersion = "1.1.2";
        public const string kToggleToolActionName = "ToggleMunicipalAreasTool";

        public static InterfaceLanguage EffectiveLanguage
        {
            get
            {
                if (CurrentSetting != null && CurrentSetting.Language > 0 && CurrentSetting.Language <= (int)InterfaceLanguage.TraditionalChinese)
                    return (InterfaceLanguage)CurrentSetting.Language;
                string locale = GameManager.instance?.localizationManager?.activeLocaleId ?? "en-US";
                if (locale.StartsWith("fr", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.French;
                if (locale.StartsWith("es", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Spanish;
                if (locale.StartsWith("it", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Italian;
                if (locale.Equals("zh-HANS", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Chinese;
                if (locale.StartsWith("de", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.German;
                if (locale.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Japanese;
                if (locale.StartsWith("pl", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Polish;
                if (locale.StartsWith("pt", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Portuguese;
                if (locale.StartsWith("ko", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Korean;
                if (locale.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.Russian;
                if (locale.Equals("zh-HANT", System.StringComparison.OrdinalIgnoreCase)) return InterfaceLanguage.TraditionalChinese;
                return InterfaceLanguage.English;
            }
        }

        public static AreaUnitPreference EffectiveUnits
        {
            get
            {
                if (CurrentSetting != null && CurrentSetting.Units == (int)AreaUnitPreference.Imperial)
                    return AreaUnitPreference.Imperial;
                if (CurrentSetting != null && CurrentSetting.Units == (int)AreaUnitPreference.Metric)
                    return AreaUnitPreference.Metric;
                return GameManager.instance?.settings?.userInterface?.unitSystem == InterfaceSettings.UnitSystem.Freedom
                    ? AreaUnitPreference.Imperial : AreaUnitPreference.Metric;
            }
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

updateSystem.UpdateAt<CommuneToolPrefabSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<CommuneAreaGeometrySystem, AreaInitializeSystem>(SystemUpdatePhase.PrefabUpdate);

updateSystem.UpdateAt<CommuneAreaToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateBefore<CommuneOverlapErrorSystem, CommuneAreaToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<CommuneShortcutSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<CommunesSystem>(SystemUpdatePhase.GameSimulation);

updateSystem.UpdateAt<CommuneMembershipSystem>(SystemUpdatePhase.GameSimulation);

            updateSystem.UpdateAt<CommuneSectionRegistrar>(SystemUpdatePhase.UIUpdate);
updateSystem.UpdateAt<CommuneIdentitySystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAfter<CommuneIdentitySystem, CommuneAreaBufferQuerySystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateBefore<CommuneIdentitySystem, AreaBufferSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateBefore<CommuneDistrictIsolationSystem, CurrentDistrictSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateBefore<CommuneLabelRaycastSystem, DefaultToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<CommuneSelectionSystem, DefaultToolSystem>(SystemUpdatePhase.ToolUpdate);

updateSystem.UpdateBefore<CommuneAreaToolStyleSystem, AreaBorderRenderSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAfter<CommuneCursorOverlaySystem, AreaBorderRenderSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateBefore<CommuneLabelBufferMigrationSystem, AreaBufferSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateBefore<CommuneLabelBufferMigrationSystem, CommuneAreaBufferQuerySystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateBefore<CommuneAreaBufferQuerySystem, AreaBufferSystem>(SystemUpdatePhase.PreCulling);

updateSystem.UpdateAt<CommuneAreaMaterialSystem>(SystemUpdatePhase.Rendering);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            MigrateLegacySettings();

            m_Setting = new Setting(this);
            m_Setting.RegisterKeyBindings();
            AssetDatabase.global.LoadSettings("CitiesAreas", m_Setting, new Setting(this));
            CurrentSetting = m_Setting;
            string[] locales = { "en-US", "fr-FR", "es-ES", "it-IT", "zh-HANS", "de-DE", "ja-JP", "pl-PL", "pt-BR", "ko-KR", "ru-RU", "zh-HANT" };
            foreach (string locale in locales)
                GameManager.instance.localizationManager.AddSource(locale, new CommunesLocale(m_Setting, locale));
            m_Setting.RegisterInOptionsUI();
            m_ToggleToolAction = m_Setting.GetAction(kToggleToolActionName);
            m_ToggleToolAction.shouldBeEnabled = true;
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
                CurrentSetting = null;
            }
            m_ToggleToolAction = null;
        }

        private static void MigrateLegacySettings()
        {
            string dataPath = UnityEngine.Application.persistentDataPath;
            string legacyPath = Path.Combine(dataPath, "Communes.coc");
            string currentPath = Path.Combine(dataPath, "CitiesAreas.coc");

            try
            {
                if (!File.Exists(legacyPath))
                    return;

                if (!File.Exists(currentPath))
                {
                    string contents = File.ReadAllText(legacyPath);
                    int headerEnd = contents.IndexOfAny(new[] { '\r', '\n' });
                    if (headerEnd < 0 || contents.Substring(0, headerEnd).Trim() != "Communes")
                    {
                        log.Warn("Legacy settings file could not be migrated; leaving it in place.");
                        return;
                    }

                    string migrated = "CitiesAreas" + contents.Substring(headerEnd);
                    string temporaryPath = currentPath + ".migration";
                    File.WriteAllText(temporaryPath, migrated);
                    File.Move(temporaryPath, currentPath);
                }

                File.Delete(legacyPath);
                log.Info("Settings migrated from Communes.coc to CitiesAreas.coc.");
            }
            catch (IOException exception)
            {
                log.Warn($"Settings migration failed: {exception.Message}");
            }
            catch (System.UnauthorizedAccessException exception)
            {
                log.Warn($"Settings migration failed: {exception.Message}");
            }
        }
    }
}
