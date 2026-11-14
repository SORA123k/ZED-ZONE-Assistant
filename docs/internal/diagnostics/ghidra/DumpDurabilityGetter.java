// Dump getter function bytes + decompilation
//@category ZEDUA
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.mem.*;
import java.io.*;

public class DumpDurabilityGetter extends GhidraScript {
    public void run() throws Exception {
        String outPath = System.getenv("ZEDUA_OUT");
        if (outPath == null) outPath = "dump.txt";
        PrintWriter w = new PrintWriter(new FileWriter(outPath));

        long imageBase = currentProgram.getImageBase().getOffset();
        w.println("[IMAGEBASE] 0x" + Long.toHexString(imageBase));

        long[] rvas = new long[] { 0x146B8A0L, 0x146B8A6L };
        for (long rva : rvas) {
            long addrOff = imageBase + rva;
            Address a = toAddr(addrOff);
            w.println("");
            w.println("=== RVA 0x" + Long.toHexString(rva) + " addr 0x" + Long.toHexString(addrOff) + " ===");

            // ensure bytes are in memory
            Memory mem = currentProgram.getMemory();
            try {
                byte[] buf = new byte[256];
                int n = mem.getBytes(a, buf);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < n; i++) sb.append(String.format("%02X ", buf[i]));
                w.println("[BYTES256] " + sb.toString());
            } catch (Exception e) { w.println("[BYTES] " + e); }

            // disassemble
            try {
                disassemble(a);
            } catch (Exception e) { w.println("[DISASM] " + e); }

            try {
                Function f = getFunctionAt(a);
                if (f == null) { f = createFunction(a, null); }
                w.println("[FUNCTION] " + (f == null ? "null" : f.getName() + " @ " + f.getEntryPoint()));
            } catch (Exception e) { w.println("[FUNCTION] " + e); }

            // list instructions in first 0x200 bytes
            try {
                InstructionIterator it = currentProgram.getListing().getInstructions(a, true);
                int cnt = 0;
                while (it.hasNext() && cnt < 80) {
                    Instruction ins = it.next();
                    if (ins.getAddress().getOffset() > a.getOffset() + 0x200) break;
                    w.println("  " + ins.getAddress() + "  " + ins.toString());
                    cnt++;
                }
            } catch (Exception e) { w.println("[INSTR] " + e); }

            // decompile
            try {
                DecompInterface di = new DecompInterface();
                di.openProgram(currentProgram);
                DecompileResults res = di.decompileFunction(getFunctionAt(a), 60, monitor);
                if (res != null && res.decompileCompleted()) {
                    w.println("[DECOMPILE]");
                    w.println(res.getDecompiledFunction().getC());
                } else {
                    w.println("[DECOMPILE] failed: " + (res == null ? "null" : res.getErrorMessage()));
                }
                di.dispose();
            } catch (Exception e) { w.println("[DECOMPILE] " + e); }
        }
        w.close();
        println("DONE -> " + outPath);
    }
}
