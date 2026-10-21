using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Game.SceneFlow;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace Communes
{
    public enum InterfaceLanguage
    {
        Game, English, French, Spanish, Italian, Chinese, German, Japanese, Polish, Portuguese,
        Korean, Russian, TraditionalChinese
    }

    public enum AreaUnitPreference
    {
        Game, Metric, Imperial
    }

    [FileLocation("CitiesAreas")]
    [SettingsUIGroupOrder(kGeneralGroup, kShortcutGroup)]
    [SettingsUIShowGroupName(kGeneralGroup, kShortcutGroup)]
    [SettingsUIKeyboardAction(Mod.kToggleToolActionName, ActionType.Button,
        usages: new string[] { Usages.kMenuUsage, Usages.kToolUsage }, interactions: new string[] { "UIButton" })]
    public class Setting : ModSetting
    {
        public const string kSection = "General";
        public const string kGeneralGroup = "Preferences";
        public const string kShortcutGroup = "Shortcut";

        public Setting(IMod mod) : base(mod) { }

        [SettingsUIDropdown(typeof(Setting), nameof(GetLanguageItems))]
        [SettingsUISection(kSection, kGeneralGroup)]
        public int Language { get; set; }

        [SettingsUIDropdown(typeof(Setting), nameof(GetUnitItems))]
        [SettingsUISection(kSection, kGeneralGroup)]
        public int Units { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public string Version => Mod.kVersion;

        [SettingsUIKeyboardBinding(BindingKeyboard.M, Mod.kToggleToolActionName, ctrl: true)]
        [SettingsUISection(kSection, kShortcutGroup)]
        public ProxyBinding ToggleToolBinding { get; set; }

        public DropdownItem<int>[] GetLanguageItems() => new[]
        {
            new DropdownItem<int> { value = 0, displayName = CurrentLocaleText()[14] },
            new DropdownItem<int> { value = 1, displayName = "English" },
            new DropdownItem<int> { value = 2, displayName = "Français" },
            new DropdownItem<int> { value = 3, displayName = "Español" },
            new DropdownItem<int> { value = 4, displayName = "Italiano" },
            new DropdownItem<int> { value = 5, displayName = "简体中文" },
            new DropdownItem<int> { value = 6, displayName = "Deutsch" },
            new DropdownItem<int> { value = 7, displayName = "日本語" },
            new DropdownItem<int> { value = 8, displayName = "Polski" },
            new DropdownItem<int> { value = 9, displayName = "Português" },
            new DropdownItem<int> { value = 10, displayName = "한국어" },
            new DropdownItem<int> { value = 11, displayName = "Русский" },
            new DropdownItem<int> { value = 12, displayName = "繁體中文" },
        };

        public DropdownItem<int>[] GetUnitItems() => new[]
        {
            new DropdownItem<int> { value = 0, displayName = CurrentLocaleText()[15] },
            new DropdownItem<int> { value = 1, displayName = CurrentLocaleText()[16] },
            new DropdownItem<int> { value = 2, displayName = CurrentLocaleText()[17] },
        };

        private string[] CurrentLocaleText()
        {
            string gameLocale = GameManager.instance?.localizationManager?.activeLocaleId ?? "en-US";
            string locale = LocaleText.ForLanguage((InterfaceLanguage)Language, gameLocale);
            return LocaleText.For(locale);
        }

        public override void SetDefaults()
        {
            Language = 0;
            Units = 0;
        }
    }

    public sealed class CommunesLocale : IDictionarySource
    {
        private readonly Setting m_Setting;
        private readonly string m_Locale;

        public CommunesLocale(Setting setting, string locale) { m_Setting = setting; m_Locale = locale; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            string selectedLocale = LocaleText.ForLanguage((InterfaceLanguage)m_Setting.Language, m_Locale);
            string[] text = LocaleText.For(selectedLocale);
            var values = new Dictionary<string, string>
            {
                [m_Setting.GetSettingsLocaleID()] = "Cities Areas",
                ["NAME.TOOLS[communes.communes]"] = LocaleText.ToolTitle(selectedLocale),
                ["Assets.NAME[CommuneTool]"] = LocaleText.ToolTitle(selectedLocale),
                ["Assets.DESCRIPTION[CommuneTool]"] = text[2],
                [m_Setting.GetOptionTabLocaleID(Setting.kSection)] = text[3],
                [m_Setting.GetOptionGroupLocaleID(Setting.kGeneralGroup)] = text[4],
                [m_Setting.GetOptionGroupLocaleID(Setting.kShortcutGroup)] = text[5],
                [m_Setting.GetOptionLabelLocaleID(nameof(Setting.Language))] = text[6],
                [m_Setting.GetOptionDescLocaleID(nameof(Setting.Language))] = text[7],
                [m_Setting.GetOptionLabelLocaleID(nameof(Setting.Units))] = text[8],
                [m_Setting.GetOptionDescLocaleID(nameof(Setting.Units))] = text[9],
                [m_Setting.GetOptionLabelLocaleID(nameof(Setting.Version))] = LocaleText.VersionLabel(selectedLocale),
                [m_Setting.GetOptionDescLocaleID(nameof(Setting.Version))] = LocaleText.VersionDescription(selectedLocale),
                [m_Setting.GetOptionLabelLocaleID(nameof(Setting.ToggleToolBinding))] = text[10],
                [m_Setting.GetOptionDescLocaleID(nameof(Setting.ToggleToolBinding))] = text[11],
                [m_Setting.GetBindingKeyLocaleID(Mod.kToggleToolActionName)] = text[12],
                [m_Setting.GetBindingMapLocaleID()] = text[13],
                ["SelectedInfoPanel.TOOLTIP[CommuneEmployees]"] = LocaleText.EmployeeTooltip(selectedLocale),
            };
            string[] ui = LocaleText.UiFor(selectedLocale);
            string[] uiKeys = { "Territory", "Districts", "Population", "Area", "Density", "NoDistrict", "DistrictTerritory" };
            for (int i = 0; i < uiKeys.Length; i++) values["Communes.UI." + uiKeys[i]] = ui[i];
            return values;
        }

        public void Unload() { }
    }

    internal static class LocaleText
    {

private static readonly string[][] s_Text =
        {
            new[] { "Municipal Areas", "Municipal Areas", "Create and manage independent municipal areas alongside the game's districts.", "General", "Preferences", "Shortcut", "Interface language", "Choose the interface language. Game language follows your Cities: Skylines II language.", "Area units", "Choose the units used for area and population density.", "Toggle Municipal Areas tool", "Shortcut to open or close the Municipal Areas tool.", "Toggle tool", "Municipal Areas settings", "Game language", "Game units", "Metric", "Imperial" },
            new[] { "Zones municipales", "Zones municipales", "Créez et gérez des zones municipales indépendantes des quartiers du jeu.", "Général", "Préférences", "Raccourci", "Langue de l’interface", "Choisissez la langue. La langue du jeu suit celle de Cities: Skylines II.", "Unités de superficie", "Choisissez les unités de superficie et de densité.", "Ouvrir/fermer l’outil", "Raccourci pour ouvrir ou fermer l’outil des zones municipales.", "Basculer l’outil", "Paramètres des zones municipales", "Langue du jeu", "Unités du jeu", "Métrique", "Impérial" },
            new[] { "Áreas municipales", "Áreas municipales", "Crea y administra áreas municipales independientes de los distritos del juego.", "General", "Preferencias", "Atajo", "Idioma de la interfaz", "Elige el idioma. El idioma del juego sigue la configuración de Cities: Skylines II.", "Unidades de superficie", "Elige las unidades de superficie y densidad.", "Abrir/cerrar la herramienta", "Atajo para abrir o cerrar la herramienta de áreas municipales.", "Alternar herramienta", "Ajustes de Áreas municipales", "Idioma del juego", "Unidades del juego", "Métrico", "Imperial" },
            new[] { "Aree municipali", "Aree municipali", "Crea e gestisci aree municipali indipendenti dai distretti del gioco.", "Generale", "Preferenze", "Scorciatoia", "Lingua dell’interfaccia", "Scegli la lingua. La lingua del gioco segue quella di Cities: Skylines II.", "Unità di superficie", "Scegli le unità per superficie e densità.", "Apri/chiudi lo strumento", "Scorciatoia per aprire o chiudere lo strumento Aree municipali.", "Attiva/disattiva strumento", "Impostazioni Aree municipali", "Lingua del gioco", "Unità del gioco", "Metrico", "Imperiale" },
            new[] { "市政区域", "市政区域", "创建和管理独立于游戏行政区的市政区域。", "常规", "偏好设置", "快捷键", "界面语言", "选择界面语言。游戏语言将跟随《Cities: Skylines II》的语言设置。", "面积单位", "选择面积和人口密度的单位。", "切换市政区域工具", "打开或关闭市政区域工具的快捷键。", "切换工具", "市政区域设置", "游戏语言", "游戏单位", "公制", "英制" },
            new[] { "Kommunalgebiete", "Kommunalgebiete", "Erstelle und verwalte eigenständige Kommunalgebiete zusätzlich zu den Bezirken des Spiels.", "Allgemein", "Einstellungen", "Tastenkürzel", "Oberflächensprache", "Wähle die Sprache. Die Spielsprache folgt Cities: Skylines II.", "Flächeneinheiten", "Wähle die Einheiten für Fläche und Bevölkerungsdichte.", "Werkzeug umschalten", "Tastenkürzel zum Öffnen oder Schließen des Kommunalgebiete-Werkzeugs.", "Werkzeug umschalten", "Einstellungen für Kommunalgebiete", "Spielsprache", "Spieleinheiten", "Metrisch", "Imperial" },
            new[] { "自治体エリア", "自治体エリア", "ゲーム内の地区とは独立した自治体エリアを作成、管理します。", "一般", "設定", "ショートカット", "表示言語", "言語を選択します。「ゲームの言語」はCities: Skylines IIの設定に従います。", "面積単位", "面積と人口密度の単位を選択します。", "自治体エリアツール切替", "自治体エリアツールを開閉するショートカットです。", "ツール切替", "自治体エリア設定", "ゲームの言語", "ゲームの単位", "メートル法", "ヤード・ポンド法" },
            new[] { "Obszary gminne", "Obszary gminne", "Twórz obszary gminne niezależne od dzielnic gry i zarządzaj nimi.", "Ogólne", "Preferencje", "Skrót", "Język interfejsu", "Wybierz język. Język gry korzysta z ustawień Cities: Skylines II.", "Jednostki powierzchni", "Wybierz jednostki powierzchni i gęstości zaludnienia.", "Przełącz narzędzie", "Skrót otwierający lub zamykający narzędzie obszarów gminnych.", "Przełącz narzędzie", "Ustawienia obszarów gminnych", "Język gry", "Jednostki gry", "Metryczne", "Imperialne" },
            new[] { "Áreas municipais", "Áreas municipais", "Crie e administre áreas municipais independentes dos distritos do jogo.", "Geral", "Preferências", "Atalho", "Idioma da interface", "Escolha o idioma. O idioma do jogo segue o Cities: Skylines II.", "Unidades de área", "Escolha as unidades de área e densidade populacional.", "Alternar ferramenta", "Atalho para abrir ou fechar a ferramenta de áreas municipais.", "Alternar ferramenta", "Configurações de Áreas municipais", "Idioma do jogo", "Unidades do jogo", "Métrico", "Imperial" },
            new[] { "시 자치구역", "시 자치구역", "게임 구역과 별도로 독립적인 자치구역을 만들고 관리합니다.", "일반", "환경설정", "단축키", "인터페이스 언어", "언어를 선택합니다. 게임 언어는 Cities: Skylines II 설정을 따릅니다.", "면적 단위", "면적과 인구 밀도에 사용할 단위를 선택합니다.", "도구 전환", "시 자치구역 도구를 열거나 닫는 단축키입니다.", "도구 전환", "시 자치구역 설정", "게임 언어", "게임 단위", "미터법", "야드파운드법" },
            new[] { "Муниципальные территории", "Муниципальные территории", "Создавайте и управляйте муниципальными территориями независимо от районов игры.", "Общие", "Настройки", "Быстрая клавиша", "Язык интерфейса", "Выберите язык. Язык игры соответствует настройкам Cities: Skylines II.", "Единицы площади", "Выберите единицы площади и плотности населения.", "Переключить инструмент", "Открыть или закрыть инструмент муниципальных территорий.", "Переключить инструмент", "Настройки муниципальных территорий", "Язык игры", "Единицы игры", "Метрические", "Имперские" },
            new[] { "市政區域", "市政區域", "建立並管理獨立於遊戲行政區的市政區域。", "一般", "偏好設定", "快捷鍵", "介面語言", "選擇介面語言。遊戲語言會依照《Cities: Skylines II》的設定。", "面積單位", "選擇面積與人口密度的單位。", "切換市政區域工具", "開啟或關閉市政區域工具的快捷鍵。", "切換工具", "市政區域設定", "遊戲語言", "遊戲單位", "公制", "英制" },
        };
        private static readonly string[][] s_Ui =
        {
            new[] { "Municipal area", "Districts", "Population", "Area", "Density", "No district", "District area" },
            new[] { "Territoire communal", "Quartiers", "Population", "Superficie", "Densité", "Aucun quartier", "Territoire du quartier" },
            new[] { "Área municipal", "Distritos", "Población", "Superficie", "Densidad", "Sin distrito", "Área del distrito" },
            new[] { "Area municipale", "Distretti", "Popolazione", "Superficie", "Densità", "Nessun distretto", "Area del distretto" },
            new[] { "市政区域", "行政区", "人口", "面积", "人口密度", "未分配行政区", "行政区面积" },
            new[] { "Kommunalgebiet", "Bezirke", "Bevölkerung", "Fläche", "Dichte", "Kein Bezirk", "Bezirksfläche" },
            new[] { "自治体エリア", "地区", "人口", "面積", "人口密度", "地区なし", "地区の面積" },
            new[] { "Obszar gminny", "Dzielnice", "Ludność", "Powierzchnia", "Gęstość", "Brak dzielnicy", "Powierzchnia dzielnicy" },
            new[] { "Área municipal", "Distritos", "População", "Área", "Densidade", "Sem distrito", "Área do distrito" },
            new[] { "시 자치구역", "구역", "인구", "면적", "밀도", "미지정 구역", "구역 면적" },
            new[] { "Муниципальная территория", "Районы", "Население", "Площадь", "Плотность", "Без района", "Площадь района" },
            new[] { "市政區域", "行政區", "人口", "面積", "人口密度", "未分配行政區", "行政區面積" },
        };

        private static readonly string[] s_EmployeeTooltips =
        {
            "Employees and open positions in buildings within this commune, grouped by education level. Workplace capacity depends on building type, size, zoning and density. Open positions are filled by citizens with the matching education level; unfilled positions reduce building efficiency.",
            "Employés et postes ouverts dans les bâtiments de cette commune, répartis par niveau d’instruction. La capacité d’emploi dépend du type, de la taille, du zonage et de la densité des bâtiments. Les postes sont pourvus par des citoyens ayant le niveau d’instruction correspondant ; les postes vacants réduisent l’efficacité des bâtiments.",
            "Empleados y puestos vacantes en los edificios de este municipio, agrupados por nivel educativo. La capacidad de empleo depende del tipo, el tamaño, la zonificación y la densidad de los edificios. Los puestos se cubren con ciudadanos que tienen el nivel educativo correspondiente; las vacantes reducen la eficiencia de los edificios.",
            "Dipendenti e posti vacanti negli edifici di questo comune, suddivisi per livello d’istruzione. La capacità lavorativa dipende dal tipo, dalle dimensioni, dalla zona e dalla densità degli edifici. I posti sono occupati da cittadini con il livello d’istruzione corrispondente; quelli vacanti riducono l’efficienza degli edifici.",
            "本市辖区内建筑的员工和空缺岗位，按教育水平分类。工作岗位容量取决于建筑类型、规模、分区和密度。空缺岗位会由具备相应教育水平的市民填补；岗位空缺会降低建筑效率。",
            "Beschäftigte und offene Stellen in den Gebäuden dieser Gemeinde, aufgeschlüsselt nach Bildungsniveau. Die Arbeitsplatzkapazität hängt von Gebäudetyp, Größe, Zonierung und Dichte ab. Offene Stellen werden von Einwohnern mit passendem Bildungsniveau besetzt; unbesetzte Stellen verringern die Effizienz der Gebäude.",
            "この自治体内の建物で働く従業員と求人を、教育レベル別に表示します。職場の定員は建物の種類、規模、ゾーニング、密度によって決まります。求人は該当する教育レベルの市民で補われ、未充足の職場は建物の効率を低下させます。",
            "Pracownicy i wolne miejsca pracy w budynkach tej gminy, pogrupowani według poziomu wykształcenia. Liczba miejsc pracy zależy od typu i wielkości budynku, strefy oraz gęstości zabudowy. Wolne miejsca zajmują mieszkańcy z odpowiednim wykształceniem; nieobsadzone stanowiska obniżają wydajność budynków.",
            "Empregados e vagas nos edifícios desta comuna, agrupados por nível de educação. A capacidade de emprego depende do tipo, tamanho, zoneamento e densidade dos edifícios. As vagas são preenchidas por cidadãos com o nível de educação correspondente; vagas não preenchidas reduzem a eficiência dos edifícios.",
            "이 자치구역 건물의 직원과 공석을 교육 수준별로 표시합니다. 일자리 수용량은 건물 유형, 크기, 용도 구역 및 밀도에 따라 달라집니다. 공석은 해당 교육 수준을 갖춘 시민이 채우며, 채워지지 않은 일자리는 건물 효율을 낮춥니다.",
            "Число сотрудников и открытых рабочих мест в зданиях этой коммуны с разбивкой по уровню образования. Вместимость рабочих мест зависит от типа и размера здания, зонирования и плотности застройки. Вакансии занимают жители с подходящим уровнем образования; незанятые места снижают эффективность зданий.",
            "此市政區域建築中的員工與職缺，依教育程度分類。工作場所容量取決於建築類型、規模、分區與密度。職缺會由具備相應教育程度的市民填補；未填補的職缺會降低建築效率。"
        };

        public static string EmployeeTooltip(string locale)
        {
            for (int i = 0; i < s_Languages.Length; i++)
                if (s_Languages[i] == locale) return s_EmployeeTooltips[i];
            return s_EmployeeTooltips[0];
        }
        private static readonly string[] s_Languages = { "en-US", "fr-FR", "es-ES", "it-IT", "zh-HANS", "de-DE", "ja-JP", "pl-PL", "pt-BR", "ko-KR", "ru-RU", "zh-HANT" };
        public static string ForLanguage(InterfaceLanguage language, string gameLocale)
        {
            int index = (int)language - 1;
            return index >= 0 && index < s_Languages.Length ? s_Languages[index] : gameLocale;
        }
        public static string[] For(string locale)
        {
            for (int i = 0; i < s_Languages.Length; i++) if (s_Languages[i] == locale) return s_Text[i];
            return s_Text[0];
        }
        public static string[] UiFor(string locale)
        {
            for (int i = 0; i < s_Languages.Length; i++) if (s_Languages[i] == locale) return s_Ui[i];
            return s_Ui[0];
        }

        public static string ToolTitle(string locale)
        {
            switch (locale)
            {
                case "fr-FR": return "Zones communales";
                case "es-ES": return "Áreas municipales";
                case "it-IT": return "Aree comunali";
                case "zh-HANS": return "市政区域";
                case "de-DE": return "Kommunalgebiete";
                case "ja-JP": return "自治体エリア";
                case "pl-PL": return "Obszary gminne";
                case "pt-BR": return "Áreas municipais";
                case "ko-KR": return "시 자치구역";
                case "ru-RU": return "Муниципальные территории";
                case "zh-HANT": return "市政區域";
                default: return "Municipal Areas";
            }
        }

        public static string VersionLabel(string locale)
        {
            switch (locale)
            {
                case "fr-FR": return "Version";
                case "es-ES": return "Versión";
                case "it-IT": return "Versione";
                case "zh-HANS": return "版本";
                case "de-DE": return "Version";
                case "ja-JP": return "バージョン";
                case "pl-PL": return "Wersja";
                case "pt-BR": return "Versão";
                case "ko-KR": return "버전";
                case "ru-RU": return "Версия";
                case "zh-HANT": return "版本";
                default: return "Version";
            }
        }

        public static string VersionDescription(string locale)
        {
            switch (locale)
            {
                case "fr-FR": return "Version installée du mod.";
                case "es-ES": return "Versión instalada del mod.";
                case "it-IT": return "Versione installata della mod.";
                case "zh-HANS": return "已安装的模组版本。";
                case "de-DE": return "Installierte Version der Mod.";
                case "ja-JP": return "インストールされているMODのバージョンです。";
                case "pl-PL": return "Zainstalowana wersja modyfikacji.";
                case "pt-BR": return "Versão instalada do mod.";
                case "ko-KR": return "설치된 모드 버전입니다.";
                case "ru-RU": return "Установленная версия мода.";
                case "zh-HANT": return "已安裝的模組版本。";
                default: return "Installed mod version.";
            }
        }

        public static string CommunePrefix(InterfaceLanguage language)
        {
            switch (language)
            {
                case InterfaceLanguage.French: return "Communes";
                case InterfaceLanguage.Spanish: return "Municipios";
                case InterfaceLanguage.Italian: return "Comuni";
                case InterfaceLanguage.Chinese: return "市镇";
                case InterfaceLanguage.German: return "Gemeinden";
                case InterfaceLanguage.Japanese: return "自治体";
                case InterfaceLanguage.Polish: return "Gminy";
                case InterfaceLanguage.Portuguese: return "Municípios";
                case InterfaceLanguage.Korean: return "지자체";
                case InterfaceLanguage.Russian: return "Муниципалитеты";
                case InterfaceLanguage.TraditionalChinese: return "市鎮";
                default: return "Municipalities";
            }
        }
    }
}
