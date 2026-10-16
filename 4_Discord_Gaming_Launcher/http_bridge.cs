using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HttpBridge
{
    class Program
    {
        const string SOCKS_HOST = "127.0.0.1";
        const int SOCKS_PORT = 1080;
        const int LISTEN_PORT = 8080;

        static readonly byte[] PAC_BODY = Encoding.ASCII.GetBytes(
            "function FindProxyForURL(url, host) {\n" +
            "    if (shExpMatch(host, '*discord*') ||\n" +
            "        shExpMatch(host, '*.dis.gd') ||\n" +
            "        host === 'dis.gd') {\n" +
            "        return 'PROXY 127.0.0.1:8080; DIRECT';\n" +
            "    }\n" +
            "    return 'DIRECT';\n" +
            "}\n"
        );

        static void Main(string[] args)
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, LISTEN_PORT);
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start(128);
                Console.WriteLine("HTTP Bridge running on 127.0.0.1:" + LISTEN_PORT);

                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Listener error: " + ex.Message);
            }
            finally
            {
                if (listener != null) listener.Stop();
            }
        }

        static bool IsDiscordHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            string h = host.ToLowerInvariant();
            int colon = h.IndexOf(':');
            if (colon > 0) h = h.Substring(0, colon);

            return h.Contains("discord") || h.Contains("dis.gd");
        }

        static void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            client.ReceiveTimeout = 10000;
            client.SendTimeout = 10000;

            NetworkStream clientStream = null;
            try
            {
                clientStream = client.GetStream();
                byte[] buffer = new byte[16384];
                int totalRead = 0;

                while (totalRead < buffer.Length)
                {
                    int r = clientStream.Read(buffer, totalRead, 1024);
                    if (r <= 0) break;
                    totalRead += r;
                    if (IndexOfBytes(buffer, totalRead, Encoding.ASCII.GetBytes("\r\n\r\n")) >= 0)
                        break;
                }

                if (totalRead == 0)
                {
                    client.Close();
                    return;
                }

                client.ReceiveTimeout = 0;
                client.SendTimeout = 0;

                string headerText = Encoding.GetEncoding("iso-8859-1").GetString(buffer, 0, totalRead);
                string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                {
                    client.Close();
                    return;
                }

                string[] firstParts = lines[0].Split(' ');
                if (firstParts.Length < 2)
                {
                    client.Close();
                    return;
                }

                string method = firstParts[0].ToUpperInvariant();
                string target = firstParts[1];

                if (method == "GET" && (target.Contains("proxy.pac") || target.EndsWith("/pac") || target == "/"))
                {
                    string pacHeader = "HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: application/x-ns-proxy-autoconfig\r\n" +
                                       "Content-Length: " + PAC_BODY.Length + "\r\n" +
                                       "Connection: close\r\n\r\n";
                    byte[] hdrBytes = Encoding.ASCII.GetBytes(pacHeader);
                    clientStream.Write(hdrBytes, 0, hdrBytes.Length);
                    clientStream.Write(PAC_BODY, 0, PAC_BODY.Length);
                    client.Close();
                    return;
                }

                if (method == "CONNECT")
                {
                    string host = target;
                    int port = 443;
                    int colon = target.LastIndexOf(':');
                    if (colon > 0)
                    {
                        host = target.Substring(0, colon);
                        int.TryParse(target.Substring(colon + 1), out port);
                    }

                    if (!IsDiscordHost(host))
                    {
                        client.Close();
                        return;
                    }

                    TcpClient remote = Socks5Connect(host, port);
                    if (remote != null)
                    {
                        byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                        clientStream.Write(ok, 0, ok.Length);
                        clientStream.Flush();

                        NetworkStream remoteStream = remote.GetStream();
                        Relay(client, remote, clientStream, remoteStream);
                    }
                    else
                    {
                        byte[] err = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                        clientStream.Write(err, 0, err.Length);
                        client.Close();
                    }
                }
                else
                {
                    string host = "127.0.0.1";
                    int port = 80;
                    byte[] newReq = null;

                    Match m = Regex.Match(target, @"^http://([^/:]+)(?::(\d+))?(.*)$", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        host = m.Groups[1].Value;
                        int p;
                        if (m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out p)) port = p;
                        string path = string.IsNullOrEmpty(m.Groups[3].Value) ? "/" : m.Groups[3].Value;
                        string newFirstLine = method + " " + path + " HTTP/1.1\r\n";
                        byte[] newFirstBytes = Encoding.ASCII.GetBytes(newFirstLine);

                        int origFirstLineLen = Encoding.GetEncoding("iso-8859-1").GetByteCount(lines[0] + "\r\n");
                        int restLen = totalRead - origFirstLineLen;
                        newReq = new byte[newFirstBytes.Length + restLen];
                        Buffer.BlockCopy(newFirstBytes, 0, newReq, 0, newFirstBytes.Length);
                        if (restLen > 0)
                        {
                            Buffer.BlockCopy(buffer, origFirstLineLen, newReq, newFirstBytes.Length, restLen);
                        }
                    }
                    else
                    {
                        for (int i = 1; i < lines.Length; i++)
                        {
                            if (lines[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                            {
                                string val = lines[i].Substring(5).Trim();
                                int c = val.IndexOf(':');
                                if (c > 0)
                                {
                                    host = val.Substring(0, c);
                                    int.TryParse(val.Substring(c + 1), out port);
                                }
                                else
                                {
                                    host = val;
                                }
                                break;
                            }
                        }
                        newReq = new byte[totalRead];
                        Buffer.BlockCopy(buffer, 0, newReq, 0, totalRead);
                    }

                    if (!IsDiscordHost(host))
                    {
                        client.Close();
                        return;
                    }

                    TcpClient remote = Socks5Connect(host, port);
                    if (remote != null)
                    {
                        NetworkStream remoteStream = remote.GetStream();
                        remoteStream.Write(newReq, 0, newReq.Length);
                        remoteStream.Flush();
                        Relay(client, remote, clientStream, remoteStream);
                    }
                    else
                    {
                        byte[] err = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                        clientStream.Write(err, 0, err.Length);
                        client.Close();
                    }
                }
            }
            catch
            {
                try { if (client != null) client.Close(); } catch { }
            }
        }

        static int IndexOfBytes(byte[] source, int length, byte[] pattern)
        {
            for (int i = 0; i <= length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        static TcpClient DirectConnect(string destHost, int destPort)
        {
            TcpClient direct = null;
            try
            {
                direct = new TcpClient();
                direct.ReceiveTimeout = 10000;
                direct.SendTimeout = 10000;
                direct.Connect(destHost, destPort);
                direct.ReceiveTimeout = 0;
                direct.SendTimeout = 0;
                return direct;
            }
            catch
            {
                if (direct != null) try { direct.Close(); } catch { }
                return null;
            }
        }

        static TcpClient Socks5Connect(string destHost, int destPort)
        {
            TcpClient socks = null;
            try
            {
                socks = new TcpClient();
                socks.ReceiveTimeout = 10000;
                socks.SendTimeout = 10000;
                socks.Connect(SOCKS_HOST, SOCKS_PORT);
                NetworkStream stream = socks.GetStream();

                byte[] greet = new byte[] { 0x05, 0x01, 0x00 };
                stream.Write(greet, 0, greet.Length);

                byte[] resp = new byte[2];
                int read = ReadExact(stream, resp, 2);
                if (read != 2 || resp[0] != 0x05 || resp[1] != 0x00)
                {
                    socks.Close();
                    return null;
                }

                byte[] hostBytes = Encoding.ASCII.GetBytes(destHost);
                byte[] req = new byte[4 + 1 + hostBytes.Length + 2];
                req[0] = 0x05;
                req[1] = 0x01;
                req[2] = 0x00;
                req[3] = 0x03;
                req[4] = (byte)hostBytes.Length;
                Buffer.BlockCopy(hostBytes, 0, req, 5, hostBytes.Length);
                req[5 + hostBytes.Length] = (byte)((destPort >> 8) & 0xFF);
                req[6 + hostBytes.Length] = (byte)(destPort & 0xFF);

                stream.Write(req, 0, req.Length);

                byte[] replyHdr = new byte[4];
                read = ReadExact(stream, replyHdr, 4);
                if (read != 4 || replyHdr[1] != 0x00)
                {
                    socks.Close();
                    return null;
                }

                byte atyp = replyHdr[3];
                if (atyp == 0x01)
                {
                    byte[] addr = new byte[4 + 2];
                    ReadExact(stream, addr, 6);
                }
                else if (atyp == 0x03)
                {
                    int len = stream.ReadByte();
                    if (len < 0) { socks.Close(); return null; }
                    byte[] addr = new byte[len + 2];
                    ReadExact(stream, addr, len + 2);
                }
                else if (atyp == 0x04)
                {
                    byte[] addr = new byte[16 + 2];
                    ReadExact(stream, addr, 18);
                }

                socks.ReceiveTimeout = 0;
                socks.SendTimeout = 0;
                return socks;
            }
            catch
            {
                if (socks != null) try { socks.Close(); } catch { }
                return null;
            }
        }

        static int ReadExact(NetworkStream s, byte[] buf, int count)
        {
            int total = 0;
            while (total < count)
            {
                int r = s.Read(buf, total, count - total);
                if (r <= 0) break;
                total += r;
            }
            return total;
        }

        static void Relay(TcpClient client, TcpClient remote, NetworkStream clientStream, NetworkStream remoteStream)
        {
            ManualResetEvent done = new ManualResetEvent(false);
            int closed = 0;

            Action<NetworkStream, NetworkStream> pipe = (src, dst) =>
            {
                byte[] buf = new byte[16384];
                try
                {
                    while (true)
                    {
                        int r = src.Read(buf, 0, buf.Length);
                        if (r <= 0) break;
                        dst.Write(buf, 0, r);
                    }
                }
                catch { }
                finally
                {
                    if (Interlocked.Increment(ref closed) >= 1)
                    {
                        try { client.Close(); } catch { }
                        try { remote.Close(); } catch { }
                        done.Set();
                    }
                }
            };

            ThreadPool.QueueUserWorkItem(o => pipe(clientStream, remoteStream));
            ThreadPool.QueueUserWorkItem(o => pipe(remoteStream, clientStream));

            done.WaitOne();
        }
    }
}
