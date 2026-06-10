using System;
using UnityEngine;
using System.Globalization;

public class GameTimeSystem : MonoBehaviour
{
    public static GameTimeSystem Instance { get; private set; }

    [Header("Start Date")]
    public int startYear = 2050;
    public int startMonth = 7;
    public int startDay = 1;

    [Header("Time Scale")]
    [Tooltip("Ile sekund gry mija w 1 sekundzie realnej")]
    public float timeScale = 60f; // 1s = 1 minuta

    private DateTime _gameTime;

    public DateTime CurrentTime => _gameTime;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _gameTime = new DateTime(
            startYear,
            startMonth,
            startDay,
            0, 0, 0,
            DateTimeKind.Utc
        );

        _lastMinute = (int)TotalMinutesSinceStart;
        _lastDate = _gameTime.Date;

    }

    void Update()
    {
        _gameTime = _gameTime.AddSeconds(Time.deltaTime * timeScale);

        int m = (int)TotalMinutesSinceStart;
        if (m != _lastMinute)
        {
            _lastMinute = m;
            OnMinuteChanged?.Invoke(m);

            if (_gameTime.Date != _lastDate)
            {
                _lastDate = _gameTime.Date;
                OnDayChanged?.Invoke(_lastDate);
            }
        }

    }

    // ===== Helpery =====

    public string GetTimeHM()
        => _gameTime.ToString("HH:mm");

    public string GetDateDMY()
        => _gameTime.ToString("dd-MM-yyyy");

    public string GetDayShort()
        => _gameTime.ToString("ddd", CultureInfo.InvariantCulture); // Mon, Tue, ...

    public void SetTime(DateTime dtUtc)
    {
        // zak³adamy UTC, ¿eby wszystko by³o spójne
        if (dtUtc.Kind != DateTimeKind.Utc)
            dtUtc = DateTime.SpecifyKind(dtUtc, DateTimeKind.Utc);

        _gameTime = dtUtc;

        // zsynchronizuj tickery (wa¿ne dla systemów opartych o eventy)
        _lastMinute = (int)TotalMinutesSinceStart;
        _lastDate = _gameTime.Date;

        // natychmiast powiadom s³uchaczy (bank/NPC/UI)
        OnMinuteChanged?.Invoke(_lastMinute);
        OnDayChanged?.Invoke(_lastDate);
    }

    public long TotalMinutesSinceStart
    {
        get
        {
            var start = new DateTime(startYear, startMonth, startDay, 0, 0, 0, DateTimeKind.Utc);
            var diff = _gameTime - start;
            return (long)Math.Floor(diff.TotalMinutes);
        }
    }

    public int MinuteOfDay => _gameTime.Hour * 60 + _gameTime.Minute;
    public int Hour => _gameTime.Hour;
    public int Minute => _gameTime.Minute;
    public DateTime Date => _gameTime.Date;

    // eventy (opcjonalnie, ale mega wygodne do NPC/Bank)
    public event Action<int> OnMinuteChanged;   // podaje TotalMinutesSinceStart
    public event Action<DateTime> OnDayChanged; // nowa data

    private int _lastMinute = -1;
    private DateTime _lastDate;

}


