using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Be.IO;
using Ionic.Zlib;

namespace Gobtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                string path = arg + " extracted";
                Directory.CreateDirectory(path);
                extract(arg, path);
            }
            Console.ReadKey();
        }

        class FileInfo
        {
            public string fileName;

            public List<BlockInfo> blocks = new List<BlockInfo>();
        }
        class BlockInfo
        {
            public int offset;
            public int length;
            public int dummy;
        }

        static void extract(string file,string directory)
        {
            List<FileInfo> list = new List<FileInfo>();

            string gobFile = file.Replace(".gfc", ".gob");
            string debugFile = file.Replace(".gfc", ".txt");
            StringBuilder sb = new StringBuilder();
            using (FileStream fs = new FileStream(file,FileMode.Open), fs2 = new FileStream(gobFile, FileMode.Open))
            {
                using (BinaryReader br2 = new BinaryReader(fs2)) { 
                    using (BeBinaryReader br = new BeBinaryReader(fs))
                    {
                        int sign = br.ReadInt32();
                        int gobSize = br.ReadInt32();
                        //int blocks = br.ReadInt32();
                        //int files = br.ReadInt32();

                        Console.WriteLine($"Reading {file}: {sign} sign, {gobSize} gobsize");
                    
                        int size = 0;
                        //while (16384 == dummy)
                        FileInfo currentFile = new FileInfo(); 
                        while (size != -1)
                        {
                            size = br.ReadInt32();
                            int offset = br.ReadInt32();
                            int dummy = br.ReadInt32();
                            
                            if(size != -1)
                            {
                                if (dummy == 16384)
                                {
                                    currentFile.blocks.Add(new BlockInfo { length = size, offset = offset, dummy=dummy });
                                    list.Add(currentFile);
                                    currentFile = new FileInfo();
                                } else
                                {
                                    // Add to last one
                                    currentFile.blocks.Add(new BlockInfo { length = size, offset = offset, dummy = dummy });
                                }
                                //sb.AppendLine($"File: size {size}, offset {offset}, dummy {dummy}");

                            }
                            //Console.WriteLine($"File: size {size}, offset {offset}, dummy {dummy}");
                        }

                        Console.WriteLine(list.Count);
                        Console.ReadKey();

                        /*// Skip empty -1 filesize lines 
                        int firstNumber = -1;
                        int emptyLines = 0;
                        while(firstNumber == -1)
                        {
                            firstNumber = br.ReadInt32();
                            int dummy2 = br.ReadInt32();
                            int dummy3 = br.ReadInt32();
                            emptyLines++;
                        }
                        // Rewind 3 integers.
                        br.BaseStream.Seek(- sizeof(int) * 3,SeekOrigin.Current);

                        sb.AppendLine($"Skipped {emptyLines} empty lines with filesize -1. Current position: {br.BaseStream.Position}");
                        */


                        // Random. Just hardcoded jump to the position where filenames are
                        br.BaseStream.Seek(348168, SeekOrigin.Begin);
                        //204 205
                        List<byte> stringTmp = new List<byte>();
                        byte tmpByte;
                        long lastStartOffset = br.BaseStream.Position;
                        try {
                            for(int i=0;i<list.Count;i++)
                            {
                                long startOffset = br.BaseStream.Position;
                                while (true)
                                {
                                    tmpByte = br.ReadByte();
                                    if(tmpByte == 0)
                                    {
                                        // end of filename reached.
                                        break;
                                    } else
                                    {
                                        stringTmp.Add(tmpByte);
                                    }
                                }
                                list[i].fileName = Encoding.ASCII.GetString(stringTmp.ToArray());

                                stringTmp.Clear();

                                long lastLength = startOffset - lastStartOffset;
                                lastStartOffset = startOffset;

                                List<byte> decompressedFileData = new List<byte>();
                                int index = 0;
                                foreach(BlockInfo bi in list[i].blocks)
                                {
                                    br2.BaseStream.Seek(bi.offset, SeekOrigin.Begin);
                                    byte[] fileContents = br2.ReadBytes(bi.length);
                                    decompressedFileData.AddRange(DecompressFile(fileContents));
                                    sb.AppendLine($"File: name {list[i].fileName}, fragment {index} , size {bi.length}, offset {bi.offset}, dummy {bi.dummy}, lastlength {lastLength}");
                                    Console.WriteLine($"File: name {list[i].fileName}, fragment {index} , size {bi.length}, offset {bi.offset}, dummy {bi.dummy}, lastlength {lastLength}");
                                    index++;
                                }

                                string pathToSave = list[i].fileName.Replace(".\\", directory + "\\");
                                string dirname = Path.GetDirectoryName(pathToSave);
                                Directory.CreateDirectory(dirname);
                                File.WriteAllBytes(pathToSave, decompressedFileData.ToArray());

                                br.BaseStream.Seek(startOffset+72,SeekOrigin.Begin);

                                /*tmpByte = br.ReadByte();
                                while ((tmpByte == 204 || tmpByte == 205) && br.BaseStream.Position < br.BaseStream.Length) {
                                
                                    tmpByte = br.ReadByte();
                                }

                                // Jump one back
                                br.BaseStream.Seek(7, SeekOrigin.Current); // 8 bytes forward. 1 byte already started in earlier loop.*/



                                if(br.BaseStream.Position == br.BaseStream.Length - 1)
                                {
                                    break; // Done. All files read.
                                }

                            }
                        } catch (Exception e)
                        {
                            // Inevitably we will reach the end of the file.. ahem
                        }

                    }
                }
            }

            File.WriteAllText(debugFile,sb.ToString());
        }

        private static byte[] DecompressFile(byte[] data)
        {
            byte type = data[4];
            byte[] inputReal = new byte[data.Length - 9];
            Array.Copy(data,5,inputReal,0,data.Length-9);

            if(type == '0')
            {
                return inputReal;
            } else
            {
                using MemoryStream compressedFileStream = new MemoryStream(inputReal);
                using MemoryStream outputFileStream = new MemoryStream();
                using var decompressor = new ZlibStream(compressedFileStream,Ionic.Zlib.CompressionMode.Decompress);
                decompressor.CopyTo(outputFileStream);
                return outputFileStream.ToArray();
            }

            
        }

    }

}
