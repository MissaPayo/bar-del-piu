namespace BarDelPiu.Audio
{
    /// <summary>
    /// Efectos de sonido. El nombre en snake_case coincide con el archivo en Assets/Audio/SFX
    /// (TapOpen -> tap_open.wav); así la librería se arma sola desde el menú de configuración.
    /// </summary>
    public enum Sfx
    {
        TapSelect,
        TapOpen,
        TapClose,
        PourLoop,
        FoamFizzLoop,
        GlassPlace,
        GlassSlide,
        ServePerfect,
        ServeGood,
        ServeRegular,
        ServeBad,
        ComboUp,
        TipCoin,
        CustomerArrive,
        CustomerHappy,
        CustomerImpatient,
        CustomerLeaveAngry,
        Error,
        PatienceWarning,
        TimerTick,
        CountdownBeep,
        CountdownGo,
        GameOver,
        NewRecord,
        UiClick,
        UiHover,
        UiBack,
        DialogBlip,
        LightOn,
        NeonOn,
        Whoosh,
        Pop,
        ScoreTick,
        PartyHorn,
        Cheer,
        CandleBlow,
        CarPass,
        Hiccup,
        Gulp,
        GlassBreak,
        LampFlicker,
        HardcoreSting,
        TimeWarning,
    }

    public enum MusicTrack
    {
        None,
        Menu,
        Game,
        Results,
        Birthday,
        Hardcore,
        BirthdayMetal,
    }

    public enum AudioBus
    {
        Master,
        Music,
        Ambience,
        Sfx,
        Ui,
    }
}
