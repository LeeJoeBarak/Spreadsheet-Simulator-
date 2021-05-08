using System;


namespace Simulator
{
    class Program
    {
        static void Main(string[] args)
        {
            SharableSpreadaheet sharedSpreadsheet = new SharableSpreadaheet(4, 5);
                 string fileName = @"D:\VisualStudioProjects\HP-main\Simulator\Simulator\outputText.txt";

          
            int col = 0;
            int row = 0;
            string str="hello";


            sharedSpreadsheet.setCell(1,2,str);

            sharedSpreadsheet.searchString(str, ref row, ref col);//REQUIRED!

     //       sharedSpreadsheet.searchInRow(1, str, ref col);

            sharedSpreadsheet.searchInCol(1, str, ref col);

                sharedSpreadsheet.save(fileName);
                sharedSpreadsheet.load(fileName);


        }
    }
}
