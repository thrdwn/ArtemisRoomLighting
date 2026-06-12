namespace Artemis.Plugins.DirectDevices;

public static class RazerReports
{
    private const int ReportLength = 91;

    public static byte[] CreateCustomFrameExtended(byte transactionId, byte row, byte startColumn, byte stopColumn, byte[] rgb)
    {
        int rowLength = ((stopColumn + 1) - startColumn) * 3;
        byte[] report = CreateReport(transactionId, 0x0F, 0x03, (byte)(rowLength + 5));

        report[9 + 2] = row;
        report[9 + 3] = startColumn;
        report[9 + 4] = stopColumn;
        Array.Copy(rgb, 0, report, 9 + 5, Math.Min(rowLength, rgb.Length));

        report[89] = CalculateCrc(report);
        return report;
    }

    public static byte[] CreateCustomModeExtended(byte transactionId)
    {
        byte[] report = CreateReport(transactionId, 0x0F, 0x02, 0x0C);
        report[9] = 0x00;
        report[10] = 0x00;
        report[11] = 0x08;
        report[89] = CalculateCrc(report);
        return report;
    }

    private static byte[] CreateReport(byte transactionId, byte commandClass, byte commandId, byte dataSize)
    {
        byte[] report = new byte[ReportLength];
        report[0] = 0x00;
        report[1] = 0x00;
        report[2] = transactionId;
        report[3] = 0x00;
        report[4] = 0x00;
        report[5] = 0x00;
        report[6] = dataSize;
        report[7] = commandClass;
        report[8] = commandId;
        return report;
    }

    private static byte CalculateCrc(byte[] report)
    {
        byte crc = 0;
        for (int i = 3; i < 89; i++)
            crc ^= report[i];
        return crc;
    }
}
