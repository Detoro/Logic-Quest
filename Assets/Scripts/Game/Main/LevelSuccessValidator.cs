using System.Linq;
using DLS.Description;
using DLS.Game;
using System.Collections.Generic;
using DLS.Simulation;

namespace DLS.Game
{
    public static class LevelSuccessValidator
    {
        // Helper to check if a wire connects a specific source pin to a specific target pin
        private static bool IsConnected(WireInstance wire, PinInstance sourcePin, PinInstance targetPin)
        {
            return wire.SourceConnectionInfo.pin == sourcePin && wire.TargetConnectionInfo.pin == targetPin;
        }
        private static int RunSimulationTest(DevChipInstance circuit, DevPinInstance sPin, DevPinInstance d1Pin, DevPinInstance d0Pin, int sVal, int d1Val, int d0Val)
        {
            const ushort CONNECTED_FLAG = 0;

            // 1. Set the PlayerInputState on the DevPins
            PinState.Set(ref sPin.Pin.PlayerInputState, (ushort)sVal, CONNECTED_FLAG);
            PinState.Set(ref d1Pin.Pin.PlayerInputState, (ushort)d1Val, CONNECTED_FLAG);
            PinState.Set(ref d0Pin.Pin.PlayerInputState, (ushort)d0Val, CONNECTED_FLAG);

            SimPin sSimPin = circuit.SimChip.InputPins.First(p => p.ID == sPin.ID);
            SimPin d1SimPin = circuit.SimChip.InputPins.First(p => p.ID == d1Pin.ID);
            SimPin d0SimPin = circuit.SimChip.InputPins.First(p => p.ID == d0Pin.ID);
            
            sSimPin.State = sPin.Pin.PlayerInputState;
            d1SimPin.State = d1Pin.Pin.PlayerInputState;
            d0SimPin.State = d0Pin.Pin.PlayerInputState;
            
            SimPin qSimPin = circuit.SimChip.OutputPins[0];
            
            // PinState.GetBitStates returns the lowest 16 bits of the state, which for a 1-bit pin is the 0 or 1 value.
            return PinState.GetBitStates(qSimPin.State);
        }

        public static bool CheckLevel1_NAND_Gate(DevChipInstance circuit)
        {
            // Component count and type check
            var allDevPins = circuit.Elements.OfType<DevPinInstance>().ToList();

            var devInputPins = allDevPins
                .Where(p => p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();

            var devOutputPins = allDevPins
                .Where(p => !p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();
            
            var nandGates = circuit.GetSubchips()
                .Where(c => c.ChipType == ChipType.Nand)
                .ToList();

            // Must have exactly 1 NAND gate, 2 DevPin Inputs, 1 DevPin Output
            if (nandGates.Count != 1 || devInputPins.Count != 2 || devOutputPins.Count != 1)
            {
                return false;
            }

            var nand = nandGates[0];
            var nandInputPins = new HashSet<PinInstance>(nand.InputPins);
            var nandOutputPin = nand.OutputPins.FirstOrDefault(); 
            var finalDevOutputPin = devOutputPins[0].Pin;
            
            if (nand.InputPins.Length != 2 || nandOutputPin == null) 
            {
                return false; 
            }

            // Check Input Connections
            var devSourcePins = new HashSet<PinInstance>(devInputPins.Select(p => p.Pin));
            var successfullyConnectedNANDInputs = new HashSet<PinInstance>();

            foreach(var wire in circuit.Wires)
            {
                var sourcePin = wire.SourceConnectionInfo.pin;
                var targetPin = wire.TargetConnectionInfo.pin;
                
                // Connection must be: DevPin_Input (Source) -> NAND_Input (Target)
                if (devSourcePins.Contains(sourcePin) && nandInputPins.Contains(targetPin))
                {
                    successfullyConnectedNANDInputs.Add(targetPin);
                }
            }

            // Check if both NAND input pins received exactly one unique connection 
            if (successfullyConnectedNANDInputs.Count != 2)
            {
                return false;
            }
            
            // Check if there is a wire connecting NAND_Output (Source) -> DevPin_Output (Target)
            bool outputConnected = circuit.Wires.Any(wire =>
                IsConnected(wire, nandOutputPin, finalDevOutputPin)
            );

            return outputConnected;
        }

        public static bool CheckLevel2_CustomGate(DevChipInstance circuit)
        {
            var allDevPins = circuit.Elements.OfType<DevPinInstance>().ToList();

            var devInputPins = allDevPins
                .Where(p => p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();

            var devOutputPins = allDevPins
                .Where(p => !p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();
            
            var nandGates = circuit.GetSubchips()
                .Where(c => c.ChipType == ChipType.Nand)
                .ToList();

            var norGates = circuit.GetSubchips()
                .Where(c => c.ChipType == ChipType.Nor)
                .ToList();

            // Must have exactly: 1 NAND, 1 NOR, 3 DevPin Inputs, 1 DevPin Output
            if (nandGates.Count != 1 || norGates.Count != 1 || devInputPins.Count != 3 || devOutputPins.Count != 1)
            {
                return false;
            }

            var nand = nandGates[0];
            var nor = norGates[0];

            // Safety check for pin counts on the gates
            if (nand.InputPins.Length != 2 || nor.InputPins.Length != 2) return false;

            var nandInputPins = new HashSet<PinInstance>(nand.InputPins);
            var norInputPins = new HashSet<PinInstance>(nor.InputPins);
            var nandOutputPin = nand.OutputPins.FirstOrDefault(); 
            var norOutputPin = nor.OutputPins.FirstOrDefault(); 
            var finalDevOutputPin = devOutputPins[0].Pin;
            
            if (nandOutputPin == null || norOutputPin == null) return false;

            // --- 2. Identify and Check Connections ---

            // A. Check NAND Input Connections (DevPin A/B -> NAND)
            var devSourcePins = new HashSet<PinInstance>(devInputPins.Select(p => p.Pin));
            var successfullyConnectedNANDInputs = new HashSet<PinInstance>();
            var unconnectedDevPins = new HashSet<PinInstance>(devSourcePins); // Start with all 3 dev pins

            // Find all wires going from a DevPin to a NAND input pin
            foreach(var wire in circuit.Wires)
            {
                var sourcePin = wire.SourceConnectionInfo.pin;
                var targetPin = wire.TargetConnectionInfo.pin;
                
                if (devSourcePins.Contains(sourcePin) && nandInputPins.Contains(targetPin))
                {
                    successfullyConnectedNANDInputs.Add(targetPin);
                    unconnectedDevPins.Remove(sourcePin); // The DevPin is now used by NAND
                }
            }

            // NAND must have both its inputs connected from two Dev Pins
            if (successfullyConnectedNANDInputs.Count != 2 || (devSourcePins.Count - unconnectedDevPins.Count) != 2)
            {
                return false;
            }
            
            // B. Identify the remaining DevPin (Input C)
            var devPinC = unconnectedDevPins.FirstOrDefault();
            if (devPinC == null) return false; // All 3 pins must be accounted for

            // C. Check NAND Output -> NOR Input 1
            bool nandToNorConnected = false;
            PinInstance norInputConnectedToNAND = null;

            if (circuit.Wires.Any(wire => IsConnected(wire, nandOutputPin, nor.InputPins[0])))
            {
                nandToNorConnected = true;
                norInputConnectedToNAND = nor.InputPins[0];
            }
            else if (circuit.Wires.Any(wire => IsConnected(wire, nandOutputPin, nor.InputPins[1])))
            {
                nandToNorConnected = true;
                norInputConnectedToNAND = nor.InputPins[1];
            }
            
            if (!nandToNorConnected) return false;
            
            // D. Check DevPin C -> NOR Input 2
            PinInstance norInputConnectedToDevPinC = nor.InputPins.First(p => p != norInputConnectedToNAND);
            
            bool cToNorConnected = circuit.Wires.Any(wire =>
                IsConnected(wire, devPinC, norInputConnectedToDevPinC)
            );

            if (!cToNorConnected) return false;
            
            // E. Check NOR Output -> Final DevPin Output
            bool finalOutputConnected = circuit.Wires.Any(wire =>
                IsConnected(wire, norOutputPin, finalDevOutputPin)
            );

            return finalOutputConnected;
        }

        public static bool CheckLevel3_HalfAdder(DevChipInstance circuit)
        {
            var allDevPins = circuit.Elements.OfType<DevPinInstance>().ToList();
            
            var devInputPins = allDevPins
                .Where(p => p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();

            var devOutputPins = allDevPins
                .Where(p => !p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();
            
            var nandGates = circuit.GetSubchips()
                .Where(c => c.ChipType == ChipType.Nand)
                .ToList();

            // Must have exactly: 5 NAND gates, 2 DevPin Inputs, 2 DevPin Outputs
            if (nandGates.Count != 5 || devInputPins.Count != 2 || devOutputPins.Count != 2)
            {
                return false;
            }
            
            // 2A. Check that all 10 NAND input pins are connected (5 gates * 2 inputs each = 10).
            int connectedNANDInputs = 0;
            foreach (var nand in nandGates)
            {
                // Safety check for gate pin count
                if (nand.InputPins.Length != 2) return false;
                
                foreach (var inputPin in nand.InputPins)
                {
                    // Check if this input pin is the target of any wire in the circuit
                    if (circuit.Wires.Any(w => w.TargetConnectionInfo.pin == inputPin))
                    {
                        connectedNANDInputs++;
                    }
                }
            }
            if (connectedNANDInputs != 10) return false;

            // 2B. Check that both Dev Output Pins are connected FROM a NAND gate output.
            var finalDevOutputPins = new HashSet<PinInstance>(devOutputPins.Select(p => p.Pin));
            int outputsConnectedFromNAND = 0;
            
            foreach (var wire in circuit.Wires)
            {
                if (finalDevOutputPins.Contains(wire.TargetConnectionInfo.pin))
                {
                    // Check if the source of the wire is the output pin of one of the 6 NAND gates
                    if (nandGates.Any(n => n.OutputPins[0] == wire.SourceConnectionInfo.pin))
                    {
                        outputsConnectedFromNAND++;
                    }
                }
            }
            
            // Both Dev Outputs must be fed from a NAND gate.
            if (outputsConnectedFromNAND != 2) return false;


            // 2C. Check that both Dev Input Pins are used as a source at least once.
            var devSourcePins = new HashSet<PinInstance>(devInputPins.Select(p => p.Pin));
            int usedDevInputPins = 0;
            
            foreach (var devPin in devSourcePins)
            {
                // Check if this DevPin is the source of any wire in the circuit
                if (circuit.Wires.Any(w => w.SourceConnectionInfo.pin == devPin))
                {
                    usedDevInputPins++;
                }
            }

            // Both A and B must be connected to the circuit.
            if (usedDevInputPins != 2) return false;

            return true;
        }

        // Inside LevelChecker.cs

        public static bool CheckLevel5_Multiplexer(DevChipInstance circuit)
        {
            // 1. Component Count and Type Check
            var allDevPins = circuit.Elements.OfType<DevPinInstance>().ToList();
            
            var devInputPins = allDevPins
                .Where(p => p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();
            
            var devOutputPins = allDevPins
                .Where(p => !p.IsInputPin && p.BitCount == PinBitCount.Bit1)
                .ToList();

            // A 2-to-1 MUX requires 3 single-bit inputs (S, D1, D0) and 1 single-bit output (Q).
            if (devInputPins.Count != 3 || devOutputPins.Count != 1)
            {
                return false;
            }

            // 2. Pin Mapping (by Name Convention for clarity, fall back to array indexing if names fail)
            // The actual names may vary, so this is a crucial point for a robust level check.
            // We will assume the input pins are ordered by a sensible ID or PinDescription ordering, or try to find them by name.
            
            // We will attempt to find the pins by the common names: S, D1, D0
            DevPinInstance sPin = devInputPins.FirstOrDefault(p => p.Name.Equals("S"));
            DevPinInstance d1Pin = devInputPins.FirstOrDefault(p => p.Name.Equals("D1"));
            DevPinInstance d0Pin = devInputPins.FirstOrDefault(p => p.Name.Equals("D0"));

            // If names are not found, use array indices as a fallback (assuming standard setup: S, D1, D0):
            // Index 0: S (Select), Index 1: D1, Index 2: D0
            if (sPin == null) sPin = devInputPins[0];
            if (d1Pin == null) d1Pin = devInputPins[1];
            if (d0Pin == null) d0Pin = devInputPins[2];
            
            DevPinInstance qPin = devOutputPins[0]; // The single output pin

            // 3. Truth Table Check (8 Cases for 3 inputs: S, D1, D0)
            
            // The truth table for a 2-to-1 MUX is: Q = (S' * D0) + (S * D1)
            
            for (int sVal = 0; sVal <= 1; sVal++)
            {
                for (int d1Val = 0; d1Val <= 1; d1Val++)
                {
                    for (int d0Val = 0; d0Val <= 1; d0Val++)
                    {
                        // Calculate expected output:
                        // If S=0 (LOW), Q = D0.
                        // If S=1 (HIGH), Q = D1.
                        int expectedQ = (sVal == 0) ? d0Val : d1Val;

                        // Run the simulation test and get the actual output
                        int actualQ = RunSimulationTest(circuit, sPin, d1Pin, d0Pin, sVal, d1Val, d0Val);

                        if (actualQ != expectedQ)
                        {
                            // The circuit failed for a specific input combination (S, D1, D0)
                            return false;
                        }
                    }
                }
            }

            // If all 8 combinations passed, the logic is correct.
            return true;
        }

    }
}