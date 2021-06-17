/// <summary>
/// This is where class-independent public enumerations belong.
/// </summary>
namespace HACS.Core
{
    public enum ASCIICode
    {
        /// <summary>
        /// (0x00) Null
        /// </summary>
        NUL = 00, 
        /// <summary>
        /// (0x01) Start of header
        /// </summary>
        SOH = 01, 
        /// <summary>
        /// (0x02) Start of text
        /// </summary>
        STX = 02, 
        /// <summary>
        /// (0x03) End of text
        /// </summary>
        ETX = 03,
        /// <summary>
        /// (0x04) End of transmission
        /// </summary>
        EOT = 04,
        /// <summary>
        /// (0x05) Enquiry
        /// </summary>
        ENQ = 05,
        /// <summary>
        /// (0x06) Acknowledged
        /// </summary>
        ACK = 06,
        /// <summary>
        /// (0x07) Bell
        /// </summary>
        BEL = 07,
        /// <summary>
        /// (0x08) Backspace
        /// </summary>
        BS = 08,
        /// <summary>
        ///  (0x09) Horizontal tab
        /// </summary>
        HT = 09,
        /// <summary>
        /// (0x0A) Line feed
        /// </summary>
        LF = 10,
        /// <summary>
        /// (0x0B) Vertical tab
        /// </summary>
        VT = 11,
        /// <summary>
        /// (0x0C) Form feed
        /// </summary>
        FF = 12,
        /// <summary>
        /// (0x0D) Carriage return
        /// </summary>
        CR = 13,
        /// <summary>
        /// (0x0E) Shift out
        /// </summary>
        SO = 14,
        /// <summary>
        /// (0x1F) Shift in
        /// </summary>
        SI = 15,
        /// <summary>
        /// (0x10) Data link escape
        /// </summary>
        DLE = 16,
        /// <summary>
        /// (0x11) Device control 1
        /// </summary>
        DC1 = 17,
        /// <summary>
        /// (0x12) Device control 2
        /// </summary>
        DC2 = 18,
        /// <summary>
        /// (0x13) Device control 3
        /// </summary>
        DC3 = 19,
        /// <summary>
        /// (0x14) Device control 4
        /// </summary>
        DC4 = 20,
        /// <summary>
        /// (0x15) Negative acknowledge
        /// </summary>
        NAK = 21,
        /// <summary>
        /// (0x16) Synchronous idle
        /// </summary>
        SYN = 22,
        /// <summary>
        /// (0x17) End of transmitted block
        /// </summary>
        ETB = 23,
        /// <summary>
        /// (0x18) Cancel
        /// </summary>
        CAN = 24,
        /// <summary>
        /// (0x19) End of medium
        /// </summary>
        EM = 25,
        /// <summary>
        /// (0x1A) Substitute
        /// </summary>
        SUB = 26,
        /// <summary>
        /// (0x1B) Escape
        /// </summary>
        ESC = 27,
        /// <summary>
        /// (0x1C) File separator
        /// </summary>
        FS = 28,
        /// (0x1D) <summary>
        /// Group separator
        /// </summary>
        GS = 29,
        /// <summary>
        /// (0x1E) Record separator
        /// </summary>
        RS = 30,
        /// <summary>
        /// (0x1F) Unit separator
        /// </summary>
        US = 31,
        /// <summary>
        /// (0x7F) Delete
        /// </summary>
        DEL = 127
    }
    public enum ValveState { Unknown, Closed, Opened, Closing, Opening, Other };
	public enum StopAction { TurnOff, TurnOn, None }
    public enum SwitchState { Off, On }
	public enum OnOffState { Unknown, Off, On }
    public enum AnalogInputMode { SingleEnded, Differential }
    public enum ThermocoupleType { None, K, T, J, E, N, B, C, D, R, S }
    public enum MassUnits { μmol, μg, mg, g }
}