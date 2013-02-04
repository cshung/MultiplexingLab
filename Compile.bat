@echo off
setlocal
set BASE=C:\andrewau\lab\MultiplexingLab-master
set BIN=%BASE%\Binaries\Debug
set MUXSRC=%BASE%\Sources\Multiplexer
c:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe @c:\andrewau\lab\Compiler\csc.rsp /noconfig /target:library /out:%BIN%\Common.dll %BASE%\Sources\Common\Constants.cs %BASE%\Sources\Common\Properties\AssemblyInfo.cs
c:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe @c:\andrewau\lab\Compiler\csc.rsp /noconfig /target:library /out:%BIN%\Multiplexer.dll %MUXSRC%\AcceptAsyncResult.cs %MUXSRC%\AsyncResult.cs %MUXSRC%\Channel.cs %MUXSRC%\Connection.cs %MUXSRC%\Constants.cs %MUXSRC%\FrameFragmentReader.cs %MUXSRC%\FrameHeader.cs %MUXSRC%\FrameWriter.cs %MUXSRC%\IFrameFragmentReader.cs %MUXSRC%\IFrameWriter.cs %MUXSRC%\ITransportReader.cs %MUXSRC%\ITransportWriter.cs %MUXSRC%\Logger.cs %MUXSRC%\ReadAsyncResult.cs %MUXSRC%\Receiver.cs %MUXSRC%\Sender.cs %MUXSRC%\TcpReader.cs %MUXSRC%\TcpWriter.cs %MUXSRC%\WriteAsyncResult.cs %MUXSRC%\WriteFrame.cs %MUXSRC%\Properties\AssemblyInfo.cs
c:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe @c:\andrewau\lab\Compiler\csc.rsp /noconfig /target:exe /out:%BIN%\Client.exe %BASE%\Sources\Client\Program.cs %BASE%\Sources\Client\Properties\AssemblyInfo.cs  /appconfig:%BASE%\Sources\Client\App.config /reference:%BIN%\Common.dll;%BIN%\Multiplexer.dll
c:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe @c:\andrewau\lab\Compiler\csc.rsp /noconfig /target:exe /out:%BIN%\Server.exe %BASE%\Sources\Server\Program.cs %BASE%\Sources\Server\Properties\AssemblyInfo.cs  /appconfig:%BASE%\Sources\Server\App.config /reference:%BIN%\Common.dll;%BIN%\Multiplexer.dll
pause