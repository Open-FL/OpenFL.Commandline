name: fl
branch: Release
project-name: OpenFL.Commandline
flags: NO_INFO_TO_ZIP;NO_STRUCTURE

#Build Info
solution: .\src\%project-name%.sln
buildout: .\src\%project-name%\bin\%branch%
buildcmd: msbuild {0} /t:Build /p:Configuration=%branch%
include: %buildout%\%name%.exe.config
target: %buildout%\%name%.exe
output: .\docs\latest\%name%.zip