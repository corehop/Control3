using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace Control3
{
    public class CH9329
    {
        // ========= SERIAL / BASIC SETUP =========

        public string PortName;
        public int BaudRate;

        private SerialPort serialPort;

        public CH9329(string PortName = "COM4", int BaudRate = 57600)
        {
            this.PortName = PortName;
            this.BaudRate = BaudRate;

            serialPort = new SerialPort(PortName, BaudRate);
            serialPort.Open();

            CreateMediaKeyTable();
        }

        private void SendPacket(byte[] data)
        {
            serialPort.Write(data, 0, data.Length);
            Thread.Sleep(1);
        }


        private byte[] CreatePacketArray(List<int> arrList, bool addCheckSum)
        {
            List<byte> bytes = arrList.ConvertAll(b => (byte)b);
            if (addCheckSum)
                bytes.Add((byte)(arrList.Sum() & 0xFF));
            return bytes.ToArray();
        }

        // ========= MODIFIER BITMASK ==========

        private byte modifierMask = 0;

        private void SetModifier(byte bit)
        {
            modifierMask |= bit;
        }

        private void ClearModifier(byte bit)
        {
            modifierMask &= (byte)~bit;
        }

        private byte GetModifierMask()
        {
            return modifierMask;
        }

        // ========= KEY ENUMS ==========

        public enum SpecialKeyCode : byte
        {
            ENTER = 0x28,
            ESCAPE = 0x29,
            BACKSPACE = 0x2A,
            TAB = 0x2B,
            SPACEBAR = 0x2C,
            CAPS_LOCK = 0x39,
            F1 = 0x3A,
            F2 = 0x3B,
            F3 = 0x3C,
            F4 = 0x3D,
            F5 = 0x3E,
            F6 = 0x3F,
            F7 = 0x40,
            F8 = 0x41,
            F9 = 0x42,
            F10 = 0x43,
            F11 = 0x44,
            F12 = 0x45,
            PRINTSCREEN = 0x46,
            SCROLL_LOCK = 0x47,
            PAUSE = 0x48,
            INSERT = 0x49,
            HOME = 0x4A,
            PAGEUP = 0x4B,
            DELETE = 0x4C,
            END = 0x4D,
            PAGEDOWN = 0x4E,
            RIGHTARROW = 0x4F,
            LEFTARROW = 0x50,
            DOWNARROW = 0x51,
            UPARROW = 0x52,
            APPLICATION = 0x65,
        }

        public enum Modifier : byte
        {
            LEFT_CTRL = 1 << 0,
            LEFT_SHIFT = 1 << 1,
            LEFT_ALT = 1 << 2,
            LEFT_WIN = 1 << 3,

            RIGHT_CTRL = 1 << 4,
            RIGHT_SHIFT = 1 << 5,
            RIGHT_ALT = 1 << 6,
            RIGHT_WIN = 1 << 7,
        }

        public enum MediaKey
        {
            EJECT,
            CDSTOP,
            PREVTRACK,
            NEXTTRACK,
            PLAYPAUSE,
            MUTE,
            VOLUMEDOWN,
            VOLUMEUP
        }

        private Dictionary<MediaKey, byte[]> mediaKeyTable;

        private void CreateMediaKeyTable()
        {
            mediaKeyTable = new Dictionary<MediaKey, byte[]>();
            mediaKeyTable.Add(MediaKey.EJECT, new byte[] { 0x02, 0x80, 0x00, 0x00 });
            mediaKeyTable.Add(MediaKey.CDSTOP, new byte[] { 0x02, 0x40, 0x00, 0x00 });
            mediaKeyTable.Add(MediaKey.PREVTRACK, new byte[] { 0x02, 0x20, 0x00, 0x00 });
            mediaKeyTable.Add(MediaKey.NEXTTRACK, new byte[] { 0x02, 0x10, 0x00, 0x00 });
            mediaKeyTable.Add(MediaKey.PLAYPAUSE, new byte[] { 0x02, 0x08, 0x00, 0x00 });
            mediaKeyTable.Add(MediaKey.MUTE, new byte[] { 0x02, 0x04, 0x00, 0x00 });
            mediaKeyTable.Add(MediaKey.VOLUMEDOWN, new byte[] { 0x02, 0x02, 0x00, 0x00 });
            mediaKeyTable.Add(MediaKey.VOLUMEUP, new byte[] { 0x02, 0x01, 0x00, 0x00 });
        }

        // ========= KEY SEND CORE ==========

        public void KeyDown(SpecialKeyCode key)
        {
            SendKeyboardPacket(GetModifierMask(), key);
        }

        public void KeyUp()
        {
            // release all 6 normal keys but keep modifiers
            SendKeyboardPacket(GetModifierMask(), 0, 0, 0, 0, 0, 0);
        }

        public void ReleaseAll()
        {
            modifierMask = 0;
            SendKeyboardPacket(0, 0, 0, 0, 0, 0, 0);
        }

        private void SendKeyboardPacket(byte mods, params SpecialKeyCode[] keys)
        {
            byte k1 = keys.Length > 0 ? (byte)keys[0] : (byte)0;
            byte k2 = keys.Length > 1 ? (byte)keys[1] : (byte)0;
            byte k3 = keys.Length > 2 ? (byte)keys[2] : (byte)0;
            byte k4 = keys.Length > 3 ? (byte)keys[3] : (byte)0;
            byte k5 = keys.Length > 4 ? (byte)keys[4] : (byte)0;
            byte k6 = keys.Length > 5 ? (byte)keys[5] : (byte)0;

            List<int> packet = new List<int>
            {
                0x57,0xAB,0x00,
                0x02,     // keyboard CMD
                0x08,     // LEN
                mods,     // modifier mask
                0x00,     // reserved
                k1,k2,k3,k4,k5,k6
            };

            SendPacket(CreatePacketArray(packet, true));
        }

        // ========= MODIFIER HELPERS ==========

        public void PressModifier(Modifier m)
        {
            SetModifier((byte)m);
            SendKeyboardPacket(GetModifierMask(), 0);
        }

        public void ReleaseModifier(Modifier m)
        {
            ClearModifier((byte)m);
            SendKeyboardPacket(GetModifierMask(), 0);
        }

        public void TapKey(SpecialKeyCode key)
        {
            SendKeyboardPacket(GetModifierMask(), key);
            Thread.Sleep(5);
            SendKeyboardPacket(GetModifierMask(), 0);
        }

        // ========= MEDIA KEYS ==========

        public void MediaKeyPress(MediaKey mk)
        {
            byte[] dat = mediaKeyTable[mk];
            List<int> packet = new List<int>
            {
                0x57,0xAB,0x00,
                0x03, // media CMD
                0x04,
                dat[0],dat[1],dat[2],dat[3]
            };
            SendPacket(CreatePacketArray(packet, true));
            Thread.Sleep(10);

            byte[] up = { 0x57, 0xAB, 0x00, 0x03, 0x04, 0x02, 0x00, 0x00, 0x00, 0x0B };
            SendPacket(up);
        }

        // ========= MOUSE (unchanged except cleanup) ==========

        public int LeftStatus = 0;
        public int RightStatus = 0;
        public int MiddleStatus = 0;
        public int X1Status = 0;
        public int X2Status = 0;

        public enum MouseButtonCode : byte
        {
            LEFT = 0x01,
            RIGHT = 0x02,
            MIDDLE = 0x04,
            X1 = 0x08,
            X2 = 0x10,
        }

        public void MouseMoveRel(int x, int y)
        {
            if (x > 127) { x = 127; }
            ; if (x < -128) { x = -128; }
            ; if (x < 0) { x = 0x100 + x; }
            ;
            if (y > 127) { y = 127; }
            ; if (y < -128) { y = -128; }
            ; if (y < 0) { y = 0x100 + y; }
            ;

            // ========================
            // mouseMoveRelPacketContents
            // HEAD{0x57, 0xAB} + ADDR{0x00} + CMD{0x05} + LEN{0x05} + DATA{0x01, 0x00}
            // CMD = 0x05 : USB mouse relative mode
            // ========================
            List<int> mouseMoveRelPacketListInt = new List<int> { 0x57, 0xAB, 0x00, 0x05, 0x05, 0x01, 0x00 };

            byte buttonStatus = (byte)(LeftStatus | RightStatus | MiddleStatus | X1Status | X2Status);
            mouseMoveRelPacketListInt[6] = buttonStatus;

            mouseMoveRelPacketListInt.Add((byte)(x));
            mouseMoveRelPacketListInt.Add((byte)(y));
            mouseMoveRelPacketListInt.Add(0x00);

            byte[] mouseMoveRelPacket = createPacketArray(mouseMoveRelPacketListInt, true);
            sendPacket(mouseMoveRelPacket);
        }
        private byte[] createPacketArray(List<int> arrList, bool addCheckSum)
        {
            List<byte> bytePacketList = arrList.ConvertAll(b => (byte)b);
            if (addCheckSum) bytePacketList.Add((byte)(arrList.Sum() & 0xff));
            return bytePacketList.ToArray();
        }

        public void MouseButtonDown(MouseButtonCode b)
        {
            if (b == MouseButtonCode.LEFT) LeftStatus = 1;
            if (b == MouseButtonCode.RIGHT) RightStatus = 2;
            if (b == MouseButtonCode.MIDDLE) MiddleStatus = 4;
            if (b == MouseButtonCode.X1) X1Status = 8;
            if (b == MouseButtonCode.X2) X2Status = 16;

            MouseMoveRel(0, 0);
        }

        public void MouseButtonUpAll()
        {
            byte[] packet = { 0x57, 0xAB, 0x00, 0x05, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00, 0x0D };
            LeftStatus = RightStatus = MiddleStatus = X1Status = X2Status = 0;
            SendPacket(packet);
        }

        public void MouseScroll(int count)
        {
            List<int> p = new List<int> { 0x57, 0xAB, 0x00, 0x05, 0x05, 0x01, 0x00, 0x00, 0x00, count };
            SendPacket(CreatePacketArray(p, true));
        }

        private string sendPacket(byte[] data)
        {
            string resultMessage = "";

            // Use a separate thread for serial port communication
            Thread serialThread = new Thread(() =>
            {
                serialPort.Write(data, 0, data.Length);
                Thread.Sleep(1);

                // Unset isMoving (see GlobalHook_MouseMove in MainWindow)
                // In CH9329 the flag is set to false again after the package is sent to remote. Prevents queue of movement on remote
                App.Flag.isMoving = false;
            });
            serialThread.Start();

            return resultMessage;
        }
    }
}
