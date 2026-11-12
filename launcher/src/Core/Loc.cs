using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace UmbraLauncher.Core
{
    /// <summary>English / Russian string table. XAML binds through the indexer: {Binding [Key], Source={x:Static core:Loc.I}}.</summary>
    public sealed class Loc : INotifyPropertyChanged
    {
        private static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public static Loc I { get; } = new Loc();

        private string _language = "en";

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler LanguageChanged;

        public string Language
        {
            get { return _language; }
            set
            {
                var v = value == "ru" ? "ru" : "en";
                if (v == _language) return;
                _language = v;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRu)));
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsRu => _language == "ru";

        public CultureInfo Culture => IsRu ? Ru : En;

        public string this[string key]
        {
            get
            {
                string[] v;
                if (key != null && Map.TryGetValue(key, out v)) return IsRu ? v[1] : v[0];
                return key;
            }
        }

        public static string T(string key) => I[key];

        public static string F(string key, params object[] args) => string.Format(I.Culture, I[key], args);

        public static string DetectSystemLanguage()
        {
            var name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return name == "ru" || name == "uk" || name == "be" || name == "kk" ? "ru" : "en";
        }

        private static void A(string key, string en, string ru) => Map[key] = new[] { en, ru };

        static Loc()
        {
            // Navigation & chrome
            A("NavLibrary", "LIBRARY", "БИБЛИОТЕКА");
            A("NavStats", "STATISTICS", "СТАТИСТИКА");
            A("NavSettings", "SETTINGS", "НАСТРОЙКИ");
            A("LangName", "ENGLISH", "РУССКИЙ");
            A("AddApp", "ADD APP", "ДОБАВИТЬ");
            A("Minimize", "Minimize", "Свернуть");
            A("Maximize", "Maximize", "Развернуть");
            A("Restore", "Restore", "Восстановить");
            A("Close", "Close", "Закрыть");
            A("SwitchLanguage", "Switch language", "Сменить язык");

            // Library
            A("HeroContinue", "CONTINUE", "ПРОДОЛЖИТЬ");
            A("HeroReady", "READY TO LAUNCH", "ГОТОВО К ЗАПУСКУ");
            A("Running", "RUNNING", "ЗАПУЩЕНО");
            A("Launch", "LAUNCH", "ЗАПУСТИТЬ");
            A("Details", "Details", "Подробнее");
            A("StatTotal", "TOTAL TIME", "ВСЕГО ВРЕМЕНИ");
            A("StatSessions", "SESSIONS", "СЕССИИ");
            A("StatWeek", "LAST 7 DAYS", "ЗА 7 ДНЕЙ");
            A("StatAvg", "AVG SESSION", "СРЕДНЯЯ СЕССИЯ");
            A("StatLast", "LAST LAUNCHED", "ПОСЛЕДНИЙ ЗАПУСК");
            A("StatLongest", "LONGEST SESSION", "САМАЯ ДОЛГАЯ");
            A("AllApps", "ALL APPS", "ВСЕ ПРИЛОЖЕНИЯ");
            A("SearchPlaceholder", "Search apps…", "Поиск приложений…");
            A("SortRecent", "Recent", "Недавние");
            A("SortMost", "Most played", "Больше всего");
            A("SortName", "A–Z", "А–Я");
            A("ViewGrid", "Cards", "Карточки");
            A("ViewList", "List", "Список");
            A("NoResults", "Nothing found", "Ничего не найдено");
            A("NoResultsHint", "Try a different name or clear the search.", "Попробуйте другое название или очистите поиск.");
            A("AddTileTitle", "Add app", "Добавить приложение");
            A("AddTileHint", ".exe file or shortcut", ".exe файл или ярлык");
            A("NeverLaunched", "Never launched", "Ещё не запускалось");
            A("NotFound", "FILE NOT FOUND", "ФАЙЛ НЕ НАЙДЕН");
            A("New", "NEW", "НОВОЕ");
            A("Admin", "ADMIN", "АДМИН");
            A("HoursUnit", "H", "Ч");
            A("LastLaunched", "Last launched {0}", "Последний запуск: {0}");
            A("RunningFor", "Running · {0}", "Запущено · {0}");

            // Empty state
            A("EmptyTitle", "YOUR LIBRARY IS EMPTY", "БИБЛИОТЕКА ПОКА ПУСТА");
            A("EmptyText", "Add any .exe — it lands in your menu, launches in one click and every hour you spend in it gets counted.",
                "Добавьте любой .exe — он появится в меню, будет запускаться в один клик, а каждый проведённый в нём час будет посчитан.");
            A("EmptyCta", "ADD YOUR FIRST APP", "ДОБАВИТЬ ПРИЛОЖЕНИЕ");
            A("EmptyCardLine1", "ADD", "ДОБАВИТЬ");
            A("EmptyCardLine2", "APP", "ПРИЛОЖЕНИЕ");
            A("EmptyDrop", "or drag & drop .exe files anywhere into this window", "или перетащите .exe файлы в это окно");

            // Menus
            A("MenuLaunch", "Launch", "Запустить");
            A("MenuLaunchAdmin", "Run as administrator", "Запустить от имени администратора");
            A("MenuFocus", "Switch to window", "Перейти к окну");
            A("MenuDetails", "Details", "Подробнее");
            A("MenuOpenFolder", "Open file location", "Открыть расположение файла");
            A("MenuEdit", "Edit", "Изменить");
            A("MenuRemove", "Remove from library", "Удалить из библиотеки");
            A("MenuCloseApp", "Close app", "Закрыть приложение");

            // Drawer
            A("DrawerActivity", "ACTIVITY · 14 DAYS", "АКТИВНОСТЬ · 14 ДНЕЙ");
            A("DrawerSessions", "RECENT SESSIONS", "ПОСЛЕДНИЕ СЕССИИ");
            A("DrawerNoSessions", "No sessions yet — launch the app and the timer starts automatically.",
                "Сессий пока нет — запустите приложение, и отсчёт начнётся автоматически.");
            A("SourceLauncher", "Launcher", "Лаунчер");
            A("SourceDetected", "Detected", "Обнаружено");
            A("LiveNow", "NOW", "СЕЙЧАС");
            A("Folder", "Folder", "Папка");
            A("Edit", "Edit", "Изменить");
            A("Remove", "Remove", "Удалить");
            A("CloseApp", "Close app", "Закрыть");
            A("SwitchTo", "Switch to window", "Перейти к окну");

            // Editor
            A("EditorAddTitle", "ADD APPLICATION", "ДОБАВИТЬ ПРИЛОЖЕНИЕ");
            A("EditorEditTitle", "EDIT APPLICATION", "ИЗМЕНИТЬ ПРИЛОЖЕНИЕ");
            A("EditorSubtitle", "Pick an .exe — it appears in your library and the launcher starts counting your hours.",
                "Выберите .exe — он появится в библиотеке, а лаунчер начнёт считать часы.");
            A("FieldPath", "PATH TO EXECUTABLE", "ПУТЬ К ФАЙЛУ");
            A("Browse", "Browse…", "Обзор…");
            A("FieldName", "NAME", "НАЗВАНИЕ");
            A("FieldArgs", "LAUNCH ARGUMENTS", "ПАРАМЕТРЫ ЗАПУСКА");
            A("ArgsPlaceholder", "Optional, e.g. -windowed -nosplash", "Необязательно, например -windowed -nosplash");
            A("PathPlaceholder", @"C:\Games\MyGame\game.exe", @"C:\Games\MyGame\game.exe");
            A("NamePlaceholder", "App name", "Название приложения");
            A("RunAsAdmin", "Run as administrator", "Запускать от имени администратора");
            A("RunAsAdminHint", "Windows will ask for permission (UAC) on every launch", "Windows будет запрашивать разрешение (UAC) при каждом запуске");
            A("Advanced", "Advanced options", "Дополнительные параметры");
            A("FieldWorkDir", "WORKING FOLDER", "РАБОЧАЯ ПАПКА");
            A("WorkDirPlaceholder", "Defaults to the app's own folder", "По умолчанию — папка приложения");
            A("FieldExtra", "ALSO TRACK THESE PROCESSES", "ТАКЖЕ ОТСЛЕЖИВАТЬ ПРОЦЕССЫ");
            A("ExtraPlaceholder", "e.g. GTA5.exe, FiveM.exe", "например GTA5.exe, FiveM.exe");
            A("ExtraHint", "Useful when a launcher starts the real game under a different name.", "Пригодится, если лаунчер запускает игру под другим именем.");
            A("FieldCover", "COVER IMAGE", "ОБЛОЖКА");
            A("ChooseImage", "Choose image…", "Выбрать изображение…");
            A("ClearImage", "Remove", "Убрать");
            A("NoCover", "Optional — shown on the card and in the banner", "Необязательно — показывается на карточке и баннере");
            A("Cancel", "Cancel", "Отмена");
            A("Save", "Save", "Сохранить");
            A("AddConfirm", "Add to library", "Добавить в библиотеку");
            A("ErrPathEmpty", "Choose an executable file first.", "Сначала выберите исполняемый файл.");
            A("ErrPathMissing", "File not found — check the path.", "Файл не найден — проверьте путь.");
            A("ErrDuplicate", "This app is already in your library.", "Это приложение уже есть в библиотеке.");
            A("ErrNameEmpty", "Enter a name.", "Введите название.");
            A("FilterApps", "Applications", "Приложения");
            A("FilterAll", "All files", "Все файлы");
            A("FilterImages", "Images", "Изображения");
            A("PreviewEmpty", "No file selected yet", "Файл ещё не выбран");
            A("PreviewHint", "Choose an .exe or a shortcut (.lnk)", "Выберите .exe или ярлык (.lnk)");

            // Confirm dialogs
            A("ConfirmRemoveTitle", "REMOVE APP?", "УДАЛИТЬ ПРИЛОЖЕНИЕ?");
            A("ConfirmRemoveText", "“{0}” will be removed from the library together with {1} of tracked time. The program itself stays on your computer.",
                "«{0}» будет удалено из библиотеки вместе с {1} отслеженного времени. Сама программа останется на компьютере.");
            A("ConfirmResetTitle", "RESET STATISTICS?", "СБРОСИТЬ СТАТИСТИКУ?");
            A("ConfirmResetText", "All tracked time for every app will be deleted. Your apps stay in the library.",
                "Всё отслеженное время по всем приложениям будет удалено. Приложения останутся в библиотеке.");
            A("Reset", "Reset", "Сбросить");

            // Toasts
            A("ToastAdded", "Added to library", "Добавлено в библиотеку");
            A("ToastAddedMany", "Added {0} apps", "Добавлено приложений: {0}");
            A("ToastSaved", "Changes saved", "Изменения сохранены");
            A("ToastLaunchFailed", "Couldn't launch", "Не удалось запустить");
            A("ToastLaunchCancelled", "Launch cancelled", "Запуск отменён");
            A("ToastLaunchCancelledText", "Administrator permission was not granted.", "Разрешение администратора не получено.");
            A("ToastFileMissing", "File not found", "Файл не найден");
            A("ToastFileMissingText", "The program was moved or deleted. Update its path.", "Программа перемещена или удалена. Укажите новый путь.");
            A("ToastSession", "Session saved", "Сессия сохранена");
            A("ToastTray", "Still running in the tray", "Лаунчер работает в трее");
            A("ToastTrayText", "Your time keeps being counted. Double-click the tray icon to come back.",
                "Время продолжает считаться. Дважды щёлкните по значку в трее, чтобы вернуться.");
            A("ToastExported", "Statistics exported", "Статистика экспортирована");
            A("ToastReset", "Statistics reset", "Статистика сброшена");
            A("ToastRemoved", "Removed from library", "Удалено из библиотеки");
            A("ToastError", "Something went wrong", "Что-то пошло не так");
            A("ToastSkipped", "Skipped {0} file(s): only .exe files and shortcuts are supported.", "Пропущено файлов: {0}. Поддерживаются только .exe и ярлыки.");
            A("ToastNoWindow", "No window to show", "Нет окна для перехода");
            A("ToastNoWindowText", "The app is running in the background.", "Приложение работает в фоне.");
            A("ToastAlreadyAdded", "Already in library", "Уже в библиотеке");

            // Stats
            A("StatsTitle", "STATISTICS", "СТАТИСТИКА");
            A("StatsSubtitle", "How much time you spend in each app.", "Сколько времени вы проводите в каждом приложении.");
            A("KpiTotal", "TOTAL TIME", "ВСЕГО ВРЕМЕНИ");
            A("KpiWeek", "LAST 7 DAYS", "ЗА 7 ДНЕЙ");
            A("KpiToday", "TODAY", "СЕГОДНЯ");
            A("KpiTop", "MOST PLAYED", "ЛИДЕР");
            A("KpiTopNone", "No data yet", "Пока нет данных");
            A("KpiWeekSame", "same as the previous 7 days", "как и за прошлые 7 дней");
            A("KpiWeekDelta", "{0} vs previous 7 days", "{0} к прошлым 7 дням");
            A("KpiWeekNew", "first active week", "первая активная неделя");
            A("ChartTitle", "ACTIVITY", "АКТИВНОСТЬ");
            A("Range7", "7 days", "7 дней");
            A("Range14", "14 days", "14 дней");
            A("Range30", "30 days", "30 дней");
            A("RankTitle", "TOP APPS", "ТОП ПРИЛОЖЕНИЙ");
            A("RankEmpty", "Launch something — your stats will show up here.", "Запустите что-нибудь — здесь появится статистика.");
            A("ChartEmpty", "No activity in this period", "За этот период активности нет");

            // Settings
            A("SettingsTitle", "SETTINGS", "НАСТРОЙКИ");
            A("SettingsSubtitle", "Make the launcher yours.", "Настройте лаунчер под себя.");
            A("SecAppearance", "APPEARANCE", "ВНЕШНИЙ ВИД");
            A("Language", "Language", "Язык");
            A("Accent", "Accent color", "Акцентный цвет");
            A("LauncherTitle", "Launcher name", "Название лаунчера");
            A("LauncherTitleHint", "Shown in the top-left corner and in the tray", "Отображается в левом верхнем углу и в трее");
            A("SecBehavior", "BEHAVIOR", "ПОВЕДЕНИЕ");
            A("MinimizeOnLaunch", "Minimize the launcher when an app starts", "Сворачивать лаунчер при запуске приложения");
            A("MinimizeOnLaunchHint", "Keeps your desktop clean while you play", "Чтобы окно не мешало, пока вы играете");
            A("CloseToTray", "Keep running in the tray when closed", "При закрытии оставаться в трее");
            A("CloseToTrayHint", "Needed to keep counting time while the window is closed", "Нужно, чтобы время считалось и при закрытом окне");
            A("StartWithWindows", "Start with Windows", "Запускать вместе с Windows");
            A("StartWithWindowsHint", "Starts quietly in the tray", "Запускается свёрнутым в трей");
            A("SecTracking", "TIME TRACKING", "УЧЁТ ВРЕМЕНИ");
            A("TrackExternal", "Count apps started outside the launcher", "Учитывать приложения, запущенные не через лаунчер");
            A("TrackExternalHint", "Time is counted even when you start an app from the desktop or Start menu",
                "Время считается, даже если приложение запущено с рабочего стола или из меню «Пуск»");
            A("FocusOnly", "Count only while the app window is active", "Считать время, только когда окно приложения активно");
            A("FocusOnlyHint", "Time spent minimized or in the background is not counted", "Время в свёрнутом виде или в фоне не учитывается");
            A("SecData", "DATA", "ДАННЫЕ");
            A("DataFolder", "Data folder", "Папка с данными");
            A("OpenDataFolder", "Open folder", "Открыть папку");
            A("ExportCsv", "Export to CSV", "Экспорт в CSV");
            A("ResetStats", "Reset statistics", "Сбросить статистику");
            A("SecAbout", "ABOUT", "О ПРОГРАММЕ");
            A("AboutText", "A personal launcher for any Windows program. Runs on the .NET Framework built into Windows 10 and 11 — nothing else to install.",
                "Персональный лаунчер для любых программ Windows. Работает на .NET Framework, встроенном в Windows 10 и 11, — устанавливать ничего не нужно.");
            A("Hotkeys", "Ctrl+N — add app  ·  Ctrl+F — search  ·  Esc — close panels", "Ctrl+N — добавить  ·  Ctrl+F — поиск  ·  Esc — закрыть панели");
            A("Version", "Version {0}", "Версия {0}");

            // Status bar
            A("StatusIdle", "Tracking active · waiting for apps", "Учёт времени активен · ожидание приложений");
            A("StatusRunning", "Tracking: {0}", "Идёт учёт: {0}");
            A("StatusTotal", "Total {0}", "Всего {0}");

            // Drop overlay
            A("DropTitle", "DROP TO ADD", "ОТПУСТИТЕ, ЧТОБЫ ДОБАВИТЬ");
            A("DropText", ".exe files and shortcuts", ".exe файлы и ярлыки");

            // Tray
            A("TrayOpen", "Open {0}", "Открыть {0}");
            A("TrayExit", "Exit", "Выход");
            A("TrayNoApps", "No apps yet", "Пока нет приложений");

            // Time words
            A("Today", "Today", "Сегодня");
            A("Yesterday", "Yesterday", "Вчера");
            A("Never", "Never", "Никогда");
        }
    }
}
