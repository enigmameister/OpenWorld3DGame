using System;
using UnityEngine;

public static class GameTime
{
    public static GameTimeSystem TS =>
        GameTimeSystem.Instance != null
            ? GameTimeSystem.Instance
            : UnityEngine.Object.FindFirstObjectByType<GameTimeSystem>();

    public static DateTime Now => TS != null ? TS.CurrentTime : DateTime.MinValue;
    public static int Hour => TS != null ? TS.Hour : 0;
    public static int Minute => TS != null ? TS.Minute : 0;
    public static int MinuteOfDay => TS != null ? TS.MinuteOfDay : 0;
    public static long TotalMinutes => TS != null ? TS.TotalMinutesSinceStart : 0;

    public static bool IsTimeBetweenHours(int openHour, int closeHour)
    {
        int h = Hour;
        // standard: open inclusive, close exclusive
        return openHour <= closeHour
            ? (h >= openHour && h < closeHour)
            : (h >= openHour || h < closeHour); // na wypadek nocnej zmiany 22-6
    }

    public static long NextDayAtHourMinutes(int hour, int minute = 0)
    {
        // bazujemy na TotalMinutes, czyli monotoniczny licznik
        long today = TotalMinutes / 1440;
        return (today + 1) * 1440 + hour * 60 + minute;
    }
}
