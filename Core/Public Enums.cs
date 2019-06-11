/// <summary>
/// This is where class-independent public enumerations belong.
/// </summary>
namespace HACS.Core
{
    public enum ThermocoupleTypes { None, TypeK, TypeT }

    public enum ASCIICodes { STX = 02, ETX = 03, EOT = 04, ENQ = 05, ACK = 06, NAK = 15 }

    public enum ValveStates { Unknown, Closed, Opened, Closing, Opening };

}