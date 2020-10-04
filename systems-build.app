name: fl-systems
branch: Debug
project-name: OpenFL.Commandline.Core
flags: NO_INFO_TO_ZIP;NO_STRUCTURE

#Additional Build Info
solution: .\src\OpenFL.Commandline.sln
include: %buildout%\Utility.dll;%buildout%\PluginSystem*.dll;%buildout%\OpenCL*.dll;%buildout%\OpenFL.dll

#Build Info
buildout: .\src\%project-name%\bin\%branch%\netstandard2.0
buildcmd: msbuild {0} /t:Build /p:Configuration=%branch%
target: %buildout%\%project-name%.dll
output: .\docs\latest\%name%.zip
