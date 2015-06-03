﻿/* Copyright (C) Atomix Development, Inc - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Aman Priyadarshi <aman.eureka@gmail.com>, December 2014
 * 
 * Start.cs
 *      Kernel Start point.
 *      
 *      History:
 *          19-12-14    File Created    Aman Priyadarshi
 */

using System;

using Atomix.CompilerExt;
using Atomix.CompilerExt.Attributes;

using Atomix.Assembler;
using Atomix.Assembler.x86;
using Core = Atomix.Assembler.AssemblyHelper;

using Atomix.Kernel_H.core;
using Atomix.Kernel_H.devices;
using Atomix.Kernel_H.arch.x86;
using Atomix.Kernel_H.drivers.video;
using Atomix.Kernel_H.drivers.FileSystem;

using Atomix.Kernel_H.lib;

namespace Atomix.Kernel_H
{
    /// <summary>
    /// Startpoint for x86 CPU
    /// Kernel will be organised at 0x10000
    /// </summary>
    [Kernel(CPUArch.x86, "0xC0100000")]
    public static class Startx86
    {
        [Assembly, Plug("Kernel_Main")]
        public static void main()
        {
            Core.DataMember.Add(new AsmData("[map all main.map]", ""));

            /* Multiboot Header */
            Core.DataMember.Add(new AsmData("MultibootSignature", BitConverter.GetBytes(0x1BADB002)));
            Core.DataMember.Add(new AsmData("MultibootFlags", BitConverter.GetBytes(65543)));
            Core.DataMember.Add(new AsmData("MultibootChecksum", BitConverter.GetBytes(-464433161)));
            Core.DataMember.Add(new AsmData("MultibootHeaderAddr", "dd (MultibootSignature - 0xC0000000)"));
            Core.DataMember.Add(new AsmData("MultibootLoadAddr", "dd (MultibootSignature - 0xC0000000)"));
            Core.DataMember.Add(new AsmData("MultibootLoadEndAddr", "dd (Compiler_End - 0xC0000000)"));
            Core.DataMember.Add(new AsmData("MultibootBSSEndAddr", "dd (Compiler_End - 0xC0000000)"));
            Core.DataMember.Add(new AsmData("MultibootEntryAddr", "dd (Kernel_Main - 0xC0000000)"));
            Core.DataMember.Add(new AsmData("MultibootVesaMode", BitConverter.GetBytes(0)));
            Core.DataMember.Add(new AsmData("MultibootVesaWidth", BitConverter.GetBytes(1024)));
            Core.DataMember.Add(new AsmData("MultibootVesaHeight", BitConverter.GetBytes(768)));
            Core.DataMember.Add(new AsmData("MultibootVesaDepth", BitConverter.GetBytes(32)));
            Core.DataMember.Add(new AsmData("BeforeInitialStack:", "TIMES 327680 db 0"));
            Core.DataMember.Add(new AsmData("InitialStack:", string.Empty));
            Core.DataMember.Add(new AsmData("InitialHeap:", "TIMES 0x100000 db 0"));
            #region KernelPageDirectory
            Core.DataMember.Add(new AsmData("align 0x1000", string.Empty));
            Core.DataMember.Add(new AsmData("KernelPageDirectory:", "\ndd (KernelPageTable - 0xC0000000 + 0x3)\ntimes (0x300 - 1) dd 0\ndd (KernelPageTable - 0xC0000000 + 0x3)\ntimes (1024 - 0x300 - 1) dd 0"));
            var xPageTable = new string[1024];
            for (int i = 0; i < 1024; i++)
                xPageTable[i] = ((i * 0x1000) | 0x3).ToString();
            Core.DataMember.Add(new AsmData("KernelPageTable", xPageTable));
            #endregion
            #region Paging
            //Load Page Directory Base Register.
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.ECX, SourceRef = "(KernelPageDirectory - 0xC0000000)" });
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.CR3, SourceReg = Registers.ECX });
            
            //Set PG bit in CR0 to enable paging.
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.ECX, SourceReg = Registers.CR0 });
            Core.AssemblerCode.Add(new Or { DestinationReg = Registers.ECX, SourceRef = "0x80000000" });
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.CR0, SourceReg = Registers.ECX });
            #endregion

            //Prepare for our quantum jump to Higher address
            Core.AssemblerCode.Add(new Literal("lea ecx, [Higher_Half_Kernel]"));
            Core.AssemblerCode.Add(new Jmp { DestinationRef = "ECX" });
            
            Core.AssemblerCode.Add(new Label("Higher_Half_Kernel"));
            Core.AssemblerCode.Add(new Mov { DestinationRef = "KernelPageDirectory", DestinationIndirect = true, SourceRef = "0x0" });
            Core.AssemblerCode.Add(new Literal("invlpg [0]"));

            /* Setup Kernel stack */
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.ESP, SourceRef = "InitialStack" });
            Core.AssemblerCode.Add(new Push { DestinationReg = Registers.EAX });//Push Magic Number
            Core.AssemblerCode.Add(new Push { DestinationReg = Registers.EBX });//Push Multiboot Address
            Core.AssemblerCode.Add(new Push { DestinationRef = "KernelPageDirectory" });
            Core.AssemblerCode.Add(new Push { DestinationRef = "InitialHeap" });

            /* Enable Floating Point Unit */            
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.EAX, SourceReg = Registers.CR0 });
            Core.AssemblerCode.Add(new And { DestinationReg = Registers.AX, SourceRef = "0xFFFD", Size = 16 });
            Core.AssemblerCode.Add(new Or { DestinationReg = Registers.AX, SourceRef = "0x10", Size = 16 });
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.CR0, SourceReg = Registers.EAX });
            Core.AssemblerCode.Add(new Literal("fninit"));

            /* Enable SSE */
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.EAX, SourceReg = Registers.CR0 });
            Core.AssemblerCode.Add(new And { DestinationReg = Registers.AX, SourceRef = "0xFFFB", Size = 16 });
            Core.AssemblerCode.Add(new Or { DestinationReg = Registers.AX, SourceRef = "0x2", Size = 16 });
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.CR0, SourceReg = Registers.EAX });
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.EAX, SourceReg = Registers.CR4 });
            Core.AssemblerCode.Add(new Or { DestinationReg = Registers.EAX, SourceRef = "0x600" });
            Core.AssemblerCode.Add(new Mov { DestinationReg = Registers.CR4, SourceReg = Registers.EAX });

            /* Call Kernel Start */
            Core.AssemblerCode.Add(new Cli());
            Core.AssemblerCode.Add(new Call("Kernel_Start"));
        }

        /// <summary>
        /// Kernel Call point
        /// </summary>
        /// <param name="magic">Magic Number of Multiboot</param>
        /// <param name="address">Multiboot Address</param>
        [Plug("Kernel_Start")]
        public static void Start(uint magic, uint address, uint KernelDirectory, uint InitialHeap)
        {
            /* Kernel Logger init */
            Debug.Init();

            /* Initalize Heap */
            Heap.Init(InitialHeap);
            
            /* Multiboot Info Parsing */
            Multiboot.Setup(magic, address);
            
            /* Setup Paging */
            Paging.Setup(KernelDirectory);
                        
            /* Setup GDT Again */
            GDT.Setup();
            
            /* Remap PIC */
            PIC.Setup();

            /* Setup IDT */
            IDT.Setup();

            /* Enable Interrupt */ 
            Native.Sti();

            /* Setup Scheduler */
            Scheduler.Init();
            
            /* Setup System Timer */
            Timer.Setup();
            
            /* Initialise Virtual File system */
            VirtualFileSystem.Setup();
            
            /* Mount Initial Ram FS */
            VirtualFileSystem.Mount("sys\\RamFS", 
                new InitRamFS(
                    new MemoryStream(Multiboot.RamDisk, Multiboot.RamDiskSize), 
                    0x372956C7, //Hash-1
                    0x15730A45, //Hash-2
                    0xFFFFFFF0, //Hash-3
                    0x00A09D31)); //Hash-4

            /*
             * Scheduler must be called before Timer because, 
             * just after calling timer, it will enable IRQ0 resulting in refrence call for switch task
             * Hence results in pagefault.
             */
            var System = new Process("System", KernelDirectory - 0xC0000000);

            /* System Thread */
            new Thread(System, 0, 0, 10000).Start();

            /* Initialise VBE 2.0 Driver */
            VBE.Init();
            
            //Boot Animation --> Maybe more managed in near future :D
            var NewStack2 = Heap.kmalloc(1000);
            new Thread(System, DoBoot.pAnimation, NewStack2 + 1000, 1000).Start();
            
            uint oldtime = Timer.ElapsedSeconds;
            while (true)//Do some heavy task
            {
                if (oldtime + 1 == Timer.ElapsedSeconds)
                {
                    oldtime += 1;
                    DoBoot.DoProgress();
                }
            }

            while (true)
            {
                Native.Cli();
                Native.Hlt();
            }
        }
    }
}