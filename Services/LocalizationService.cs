using System.Collections.Generic;

namespace DDNetNW.Services;

public static class LocalizationService
{
    public static string CurrentLanguage { get; set; } = "en";

    private static readonly Dictionary<string, string> En = new()
    {
        ["mainMenu"] = "Main menu",
        ["notifications"] = "Notifications",
        ["options"] = "Options",
        ["about"] = "About",
        ["online"] = "Online",
        ["offline"] = "Offline",
        ["waiting"] = "Waiting",
        ["noSession"] = "No online session detected yet.",
        ["lastSeen"] = "Last seen",
        ["waitingScan"] = "Waiting for the first scan.",
        ["watchSelected"] = "Watch selected DDNet nicknames from the public server list.",
        ["address"] = "Address",
        ["server"] = "Server",
        ["map"] = "Map",
        ["status"] = "Status",
        ["details"] = "Details",
        ["mode"] = "Mode",
        ["clan"] = "Clan",
        ["team"] = "Team",
        ["afk"] = "AFK",
        ["yes"] = "yes",
        ["no"] = "no",
        ["player"] = "player",
        ["spectator"] = "spectator",
        ["unknown"] = "unknown"
    };

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["mainMenu"] = "Главное меню",
        ["notifications"] = "Уведомления",
        ["options"] = "Настройки",
        ["about"] = "О приложении",
        ["online"] = "В сети",
        ["offline"] = "Не в сети",
        ["waiting"] = "Ожидание",
        ["noSession"] = "Онлайн-сессия пока не обнаружена.",
        ["lastSeen"] = "Последний раз в сети",
        ["waitingScan"] = "Ожидание первой проверки.",
        ["watchSelected"] = "Отслеживайте выбранные ники DDNet из публичного списка серверов.",
        ["address"] = "Адрес",
        ["server"] = "Сервер",
        ["map"] = "Карта",
        ["status"] = "Статус",
        ["details"] = "Детали",
        ["mode"] = "Режим",
        ["clan"] = "Клан",
        ["team"] = "Команда",
        ["afk"] = "AFK",
        ["yes"] = "да",
        ["no"] = "нет",
        ["player"] = "игрок",
        ["spectator"] = "наблюдатель",
        ["unknown"] = "неизвестно"
    };


    private static readonly Dictionary<string, string> Es = new()
    {
        ["mainMenu"] = "Menú principal",
        ["notifications"] = "Notificaciones",
        ["options"] = "Opciones",
        ["about"] = "Acerca de",
        ["online"] = "En línea",
        ["offline"] = "Desconectado",
        ["waiting"] = "Esperando",
        ["noSession"] = "No se detectó una sesión online todavía.",
        ["lastSeen"] = "Visto por última vez",
        ["waitingScan"] = "Esperando el primer escaneo.",
        ["watchSelected"] = "Sigue nicks de DDNet desde la lista pública de servidores.",
        ["address"] = "Dirección",
        ["server"] = "Servidor",
        ["map"] = "Mapa",
        ["status"] = "Estado",
        ["details"] = "Detalles",
        ["mode"] = "Modo",
        ["clan"] = "Clan",
        ["team"] = "Equipo",
        ["afk"] = "AFK",
        ["yes"] = "sí",
        ["no"] = "no",
        ["player"] = "jugador",
        ["spectator"] = "espectador",
        ["unknown"] = "desconocido"
    };

    public static string Get(string key)
    {
        var map = CurrentLanguage switch
        {
            "ru" => Ru,
            "es" => Es,
            _ => En
        };
        return map.TryGetValue(key, out var value) ? value : key;
    }
}
