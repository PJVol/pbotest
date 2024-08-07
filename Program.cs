using System;
using ZenStates.Core;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Reflection;
using System.Diagnostics.Eventing.Reader;
using System.Net;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            App app = new App();
            app.Start();
        }
    }

    public class CPUCores
    {
        public int
            ccd_total,
            ccd_active,
            cores_per_ccd,
            cores_count,
            core_num;
        bool isAPU,
            isVermeer,
            isRaphael;
        public int[] ID_map;
        public int[] curve;
        protected readonly Cpu cpu;
        public string cpuName;

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

        public CPUCores()
        {
            cpu = new Cpu();
            cpuName = cpu.info.cpuName;
            isAPU = cpu.info.codeName == Cpu.CodeName.Cezanne;
            isVermeer = cpu.info.codeName == Cpu.CodeName.Vermeer;
            isRaphael = cpu.info.codeName == Cpu.CodeName.Raphael;
        }

        ~CPUCores() { 
            cpu.Dispose();
        }

        public SMU.Status sendSmuCommand(Mailbox mbox, uint msg, ref uint[] args)
        {
            return cpu.smu.SendSmuCommand(mbox, msg, ref args);
        }

        public (uint, uint) getDisCores()
        {
            uint
                ccd_fuse1,
                coreDis1_m,
                coreDis2_m,
                ccd_enabled_m,
                ccd_dis_m;

            ccd_fuse1 = cpu.info.topology.ccdsPresent;
            ccd_enabled_m = BitSlice(ccd_fuse1, 22, 23);
            ccd_dis_m = BitSlice(ccd_fuse1, 30, 31);
            ccd_total = countSetBits(ccd_enabled_m);


            coreDis1_m = cpu.info.topology.coreDisableMap[0] & 0xFF;
            coreDis2_m = ccd_total > 1
                ? cpu.info.topology.coreDisableMap[1] & 0xFF
                : 0xFF;

            uint cores_layout = coreDis1_m | (coreDis2_m << 8) | 0xFFFF0000;
            core_num = countSetBits(~cores_layout);


            // in Raphael ccd_fuse_down probably not used and always zero
            ccd_active = cpu.info.codeName == Cpu.CodeName.Raphael
                ? ccd_total
                : (ccd_dis_m > 0 ? 1 : 2);

            cores_per_ccd = (coreDis1_m == 0 || coreDis2_m == 0) ? 8 : 6;

            makeMap(core_num, cores_layout);
            curve = new int[ccd_active * cores_per_ccd];
            return (ccd_fuse1, cores_layout);
        }

        public void makeMap(int num, uint layout)
        {
            uint cores_t = layout;
            ID_map = new int[num];
            for (int i = 0, k = 0; i < ccd_total * 8; i++, cores_t = cores_t >> 1)
                if ((cores_t & 1) == 0)
                    ID_map[k++] = i;
        }

        public void SetPsmMargin(int core_id = 0, int count = 0)
        {
            uint MSG = cpu.smu.Rsmu.SMU_MSG_SetDldoPsmMargin;
            uint[] args = new uint[6];
            args[0] = (uint)(((core_id & 8) << 5 | (core_id & 7)) << 20 | count & 0xFFFF);
            try
            {
#if DEBUG
            //    ConsoleLog($"Set psm margin {args[0]} for core ${core_id}");
#endif
                SMU.Status status = sendSmuCommand(cpu.smu.Rsmu, MSG, ref args);
            }
            catch (ApplicationException ex)
            {
                HandleError(ex.Message, "Error reading response");
            }
        }

        public int GetPsmMargin(int core_id)
        {
            uint MSG = cpu.smu.Rsmu.SMU_MSG_GetDldoPsmMargin;
            uint[] args = new uint[6];

            args[0] = isAPU
                ? (uint)core_id
                : (uint)((core_id & 8) << 5 | (core_id & 7)) << 20;
            try
            {
                SMU.Status status = sendSmuCommand(cpu.smu.Rsmu, MSG, ref args);
            }
            catch (ApplicationException ex)
            {
                HandleError(ex.Message, "Error reading response");
            }
            return (int)args[0];
        }

        private void HandleError(string message, string title = "Error")
        {
            Console.WriteLine("Error: " + message);
        }

        public void HandleCurve(string cmd, ref string[] curve_d)
        {

            if (cmd == "get") {
                try
                {
                    for (int i = 0; i < curve.Length; i++)
                    {
                        int cnt = GetPsmMargin(ID_map[i]);
                        curve_d[i] = cnt.ToString();
                    }
                }
                catch
                {
                    Environment.Exit(999);
                    throw new ApplicationException($"Error reading CO values: ");
                }
            }
            else if (cmd == "set")
            {
                try
                {
                    for (int i = 0; i < curve.Length; i++)
                    {
                        int count = Int32.Parse(curve_d[i]);
                        int coreID = ID_map[i];

                        SetPsmMargin(coreID, count);
                    }
                }
                catch
                {
                    Environment.Exit(999);
                    throw new ApplicationException($"Invalid 'count' values: '{curve_d}'");
                }
            }
        }

    }

    public class App
    {
        public uint
            ccd_fuse = 0,
            ccd_fuse2 = 0,
            core_fuse1 = 0,
            core_fuse2 = 0;

        public CPUCores _cpu_;
        public App() {

        }

        public void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            string res = "", cmd = "";

            _cpu_ = new CPUCores();
            _cpu_.getDisCores();

            int cores = _cpu_.core_num;
            string[] curve_data;

            cmd = (args.Length > 1) ? args[1] : "info";           
            curve_data = new string[cores];

            if (cmd == "set" && args.Length != cores + 2) 
            {
                Console.WriteLine($"Error: Wrong arguments count {args.Length}");
            } 
            else
            {
                switch (cmd)
                {
                    case "info":
                        res = _cpu_.cpuName;
                        break;
                    case "set":
                    case "get":
                        if (cmd == "set")  
                            for (int i = 0; i < cores; i++) curve_data[i] = args[i + 2];
                        _cpu_.HandleCurve(cmd, ref curve_data);
                        res = string.Join(" ", curve_data);
                        break;
                    default:
                        Console.WriteLine($"Error: unknown command: {cmd}");
                        break;
                }
                if (res != "") 
                    Console.WriteLine($"{res}");
            }
        }
    }
}
