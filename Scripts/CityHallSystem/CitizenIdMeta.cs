[System.Serializable]
public struct CitizenIdMeta
{
    public string documentId;
    public string citizenIdNumber;
    public string holderName;

    public int colorVariant;
    public string photoFilePath;

    public bool isValid;
    public bool isReportedLost;

    public long issuedAtGameMinutes;
}