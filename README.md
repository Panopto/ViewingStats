# ViewingStats
Sample project that collects viewing stats for sessions in Panopto.

This sample code shows how to use Panopto UsageReporting API.

This application does
1. enumerate up to 100 sessions of the target Panopto site
2. collects the viewing statistics between now and 30 days before for those sessions
3. collate to users
4. write the result to CSV file in current directory

Many parameters are hard coded for demonstration purpose.

This application is built by Visual Studio 2015 Professional.
Other version / edition of Visual Studio may build this, but Panopto does not confirm.
