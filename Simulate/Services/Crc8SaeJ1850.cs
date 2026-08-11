using System;

namespace Simulate.Services
{
    public static class Crc8SaeJ1850
    {
        // CRC-8/SAE-J1850 parameters used by AUTOSAR Crc_CalculateCRC8.
        // Source: https://www.autosar.org/fileadmin/standards/R24-11/CP/AUTOSAR_CP_SWS_CRCLibrary.pdf
        private const byte Polynomial = 0x1D;
        private const byte InitialValue = 0xFF;
        private const byte FinalXorValue = 0xFF;

        public static byte Calculate(ReadOnlySpan<byte> data)
        {
            byte checksum = InitialValue;
            foreach (byte value in data)
            {
                checksum ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    checksum = (checksum & 0x80) != 0
                        ? (byte)((checksum << 1) ^ Polynomial)
                        : (byte)(checksum << 1);
                }
            }

            return (byte)(checksum ^ FinalXorValue);
        }
    }
}
