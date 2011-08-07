/*
	Copyright 2011 MCForge
	
	Dual-licensed under the	Educational Community License, Version 2.0 and
	the GNU General Public License, Version 3 (the "Licenses"); you may
	not use this file except in compliance with the Licenses. You may
	obtain a copy of the Licenses at
	
	http://www.opensource.org/licenses/ecl2.php
	http://www.gnu.org/licenses/gpl-3.0.html
	
	Unless required by applicable law or agreed to in writing,
	software distributed under the Licenses are distributed on an "AS IS"
	BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
	or implied. See the Licenses for the specific language governing
	permissions and limitations under the Licenses.
*/
		
// Created by Techjar
using System;
using System.Collections.Generic;

namespace MCForge
{
   public class CmdMaze : Command
   {
      public override string name { get { return "maze"; } }
      public override string shortcut { get { return ""; } }
      public override string type { get { return "build"; } }
      public override bool museumUsable { get { return false; } }
      public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
      public override void Use(Player p, string message)
      {
         int number = message.Split(' ').Length;
         if (number > 3 || number < 2) { Help(p); return; }
         if (number == 3)
         {
            string[] m = message.Split(' ');
            string w = m[0].ToLower();
            string h = m[1].ToLower();
            string t = m[2].ToLower();
            byte type = Block.Byte(t);
            if (type == 255) { Player.SendMessage(p, "There is no block \"" + t + "\"."); return; }
            if (!Block.canPlace(p, type)) { Player.SendMessage(p, "Cannot place that."); return; }

            CatchPos cpos; cpos.type = type;
            cpos.w = Convert.ToUInt16(w); cpos.h = Convert.ToUInt16(h); p.blockchangeObject = cpos;
         }
         else
         {
            string[] m = message.Split(' ');
            string w = m[0].ToLower();
            string h = m[1].ToLower();
            
            CatchPos cpos; unchecked { cpos.type = (byte)-1; }
            cpos.w = Convert.ToUInt16(w); cpos.h = Convert.ToUInt16(h); p.blockchangeObject = cpos;
         }
         Player.SendMessage(p, "Place a block in the corner of where you want the maze.");
         p.ClearBlockchange();
         p.Blockchange += new Player.BlockchangeEventHandler(Blockchange1);
      }
      public override void Help(Player p)
      {
         Player.SendMessage(p, "/maze [width] [height] <type> - generate a random maze.");
         Player.SendMessage(p, "Note that the specified dimensions are how the algorithm sees it, the actual dimensions will be twice as large.");
      }
      
      public void Blockchange1(Player p, ushort x, ushort y, ushort z, byte type)
      {
         p.ClearBlockchange();
         byte b = p.level.GetTile(x, y, z);
         p.SendBlockchange(x, y, z, b);
         CatchPos cpos = (CatchPos)p.blockchangeObject;
         unchecked { if (cpos.type != (byte)-1) type = cpos.type; else type = p.bindings[type]; }
         List<Pos> buffer = new List<Pos>();
         Random rand = new Random();
         int rnum = 0;

         //ushort xx; ushort yy; ushort zz;
         List<ushort> stack = new List<ushort>();
         bool[] visited = new bool[cpos.w * cpos.h * 2];
         bool[] noWallLeft = new bool[cpos.w * cpos.h * 2];
         bool[] noWallAbove = new bool[cpos.w * cpos.h * 2];

         stack.Add(id(0, 0, cpos));

         while (stack.Count > 0) {
            ushort cell = pop(stack);
            ushort xx = dx(cell, cpos), yy = dy(cell, cpos);
            visited[cell] = true;
            
            List<ushort> neighbors = new List<ushort>();
            if (xx > 0) neighbors.Add(id(xx - 1, yy, cpos));
            if (xx < cpos.w - 1) neighbors.Add(id(xx + 1, yy, cpos));
            if (yy > 0) neighbors.Add(id(xx, yy - 1, cpos));
            if (yy < cpos.h - 1) neighbors.Add(id(xx, yy + 1, cpos));
            
            //shuffle(neighbors);
            
            while (neighbors.Count > 0) {
               rnum = rand.Next(neighbors.Count);
               ushort neighbor = neighbors[rnum]; neighbors.RemoveAt(rnum);
               ushort nx = dx(neighbor, cpos), ny = dy(neighbor, cpos);
               
               if (!visited[neighbor]) {
                  stack.Add(cell);
                  
                  if (yy == ny) {
                     if (nx < xx) {
                        noWallLeft[cell] = true;
                     } else {
                        noWallLeft[neighbor] = true;
                     }
                  } else {
                     if (ny < yy) {
                        noWallAbove[cell] = true;
                     } else {
                        noWallAbove[neighbor] = true;
                     }
                  }
                  
                  stack.Add(neighbor);
                  break;
               }
            }
         }
         
         for (ushort y2 = 0; y2 <= cpos.h; y2++) {
            for (ushort x2 = 0; x2 <= cpos.w; x2++) {
               ushort cell = id(x2, y2, cpos);
               if (!noWallLeft[cell] && y2 < cpos.h) {
                  BufferAdd(buffer, (ushort)(x + (x2 * 2 - 1)), y, (ushort)(z + (y2 * 2)));
                  BufferAdd(buffer, (ushort)(x + (x2 * 2 - 1)), (ushort)(y + 1), (ushort)(z + (y2 * 2)));
               }
               if (!noWallAbove[cell] && x2 < cpos.w) {
                  BufferAdd(buffer, (ushort)(x + (x2 * 2)), y, (ushort)(z + (y2 * 2 - 1)));
                  BufferAdd(buffer, (ushort)(x + (x2 * 2)), (ushort)(y + 1), (ushort)(z + (y2 * 2 - 1)));
               }
               BufferAdd(buffer, (ushort)(x + (x2 * 2 - 1)), y, (ushort)(z + (y2 * 2 - 1)));
               BufferAdd(buffer, (ushort)(x + (x2 * 2 - 1)), (ushort)(y + 1), (ushort)(z + (y2 * 2 - 1)));
            }
         }

         // Check to see if user is subject to anti-tunneling
         // This code won't compile for some reason
         /*if (Server.antiTunnel && p.group.Permission == LevelPermission.Guest && !p.ignoreGrief)
         {
            int CheckForBlocksBelowY = p.level.depth / 2 - Server.maxDepth;
            if (buffer.Any(pos => pos.y < CheckForBlocksBelowY))
            {
               p.SendMessage("You're not allowed to build this far down!");
               return;
            }
         }*/

         if (Server.forceCuboid)
         {
            int counter = 1;
            buffer.ForEach(delegate(Pos pos)
            {
               if (counter <= p.group.maxBlocks)
               {
                  counter++;
                  p.level.Blockchange(p, pos.x, pos.y, pos.z, type);
               }
            });
            if (counter >= p.group.maxBlocks)
            {
               Player.SendMessage(p, "Tried to cuboid " + buffer.Count + " blocks, but your limit is " + p.group.maxBlocks + ".");
               Player.SendMessage(p, "Executed cuboid up to limit.");
            }
            else
            {
               Player.SendMessage(p, buffer.Count.ToString() + " blocks.");
            }
            if (p.staticCommands) p.Blockchange += new Player.BlockchangeEventHandler(Blockchange1);
            return;
         }

         if (buffer.Count > p.group.maxBlocks)
         {
            Player.SendMessage(p, "You tried to cuboid " + buffer.Count + " blocks.");
            Player.SendMessage(p, "You cannot cuboid more than " + p.group.maxBlocks + ".");
            return;
         }

         Player.SendMessage(p, buffer.Count.ToString() + " blocks.");

         buffer.ForEach(delegate(Pos pos)
         {
            p.level.Blockchange(p, pos.x, pos.y, pos.z, type);
         });
         
         Player.SendMessage(p, "Maze completed!");

         if (p.staticCommands) p.Blockchange += new Player.BlockchangeEventHandler(Blockchange1);
      }
      
      void BufferAdd(List<Pos> list, ushort x, ushort y, ushort z)
      {
         Pos pos; pos.x = x; pos.y = y; pos.z = z; list.Add(pos);
      }
      
      ushort id(int x, int y, CatchPos cpos)
      {
         return (ushort)(y * (cpos.w + 1) + x);
      }

      ushort dx(ushort i, CatchPos cpos)
      {
         return (ushort)(i % (cpos.w + 1));
      }

      ushort dy(ushort i, CatchPos cpos)
      {
         return (ushort)Math.Floor((decimal)(i / (cpos.w + 1)));
      }

      void shuffle<T>(List<T> list)
      {
         Random rng = new Random();
         int n = list.Count;
         while (n > 0) {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
         }
      }
      
      T pop<T>(List<T> list)
      {
         int lastIndex = list.Count - 1; 
         T temp = list[lastIndex];
         list.RemoveAt(lastIndex);
         return temp;
      }
      
      struct Pos
      {
         public ushort x, y, z;
      }
      
      struct CatchPos
      {
         public byte type;
         public ushort w, h;
      }
   }
   
   
}
    
