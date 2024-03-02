using System;
using ZenStates.Core;
using System.Collections.Generic;
using System.Text;
using System.Threading;


namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello!");
            App app = new App();
            app.Start();
        }
    }

    public class App
    {
        public Cpu cpu = new Cpu();
        public uint
            ccd_fuse = 0,
            ccd_fuse2 = 0,
            core_fuse1 = 0,
            core_fuse2 = 0;
        public App() { }
        public void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 2)
            {
                Console.WriteLine("No CMD given. Bye!");
            }
            else
            {
                string cmd = args[1];
                if (cmd == "fuses")
                {
                    GetDisCores();
                    Console.WriteLine($"CPU: {cpu.info.cpuName} - CMD: {cmd}");
                    Console.WriteLine("-------------------------");
                    Console.WriteLine($"CCDs Fuse1 {ccd_fuse:X8}");
                    Console.WriteLine($"CCDs Fuse2 {ccd_fuse2:X8}");
                    Console.WriteLine($"Core Fuse1 {core_fuse1:X8}");
                    Console.WriteLine($"Core Fuse2 {core_fuse1:X8}");
                }
            }
            cpu.Dispose();
        }

        public void GetDisCores()
        {
            uint ccds_total,
                ccds_disabled,
                ccd1_fuse,
                ccd2_fuse;

            switch (cpu.info.codeName)
            {
                case Cpu.CodeName.Vermeer:
                    ccd_fuse = cpu.ReadDword(0x5D218);
                    ccd_fuse2 = cpu.ReadDword(0x5D21C);
                    core_fuse1 = cpu.ReadDword(0x30081D98);
                    core_fuse2 = cpu.ReadDword(0x32081D98);
                    break;

                case Cpu.CodeName.Cezanne:
                    ccd_fuse = 0x80400000;
                    core_fuse1 = (cpu.ReadDword(0x5D448) >> 11) & 0xFF | 0x300;
                    core_fuse2 = 0xFFFFFFFF;
                    break;

                case Cpu.CodeName.Raphael:
                    ccd_fuse = cpu.ReadDword(0x5D3BC);
                    ccd_fuse2 = cpu.ReadDword(0x5D3C0);
                    core_fuse1 = cpu.ReadDword(0x30081CD0);
                    core_fuse2 = cpu.ReadDword(0x32081CD0);
                    break;

                default:
                    break;
            }

            ccds_total = BitSlice(ccd_fuse, 22, 23);
            ccds_disabled = BitSlice(ccd_fuse, 30, 31);
            ccd1_fuse = BitSlice(core_fuse1, 0, 7);
            ccd2_fuse = BitSlice(core_fuse2, 0, 7);
        }
        static uint BitSlice(uint arg, int start, int end)
        {
            uint mask = (2u << (end - start)) - 1;
            return (arg >> start) & mask;
        }

        static int countSetBits(uint n)
        {
            uint count = 0;
            while (n > 0)
            {
                count += n & 1;
                n >>= 1;
            }
            return (int)count;
        }
    }
}
