using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace Simulator
{
    /*
    ---> Guidelines:
    1. implement it like it's a sinle thread program
    2. review the code and strategize where to place mutexes, 
        i.e. locate critical-sections and handle them with mutexes/Semaphores/any other Synchronization tool */

    class SharableSpreadaheet
    {
        String[][] spreadsheet;/*CONSIDER CHANGING TO A STRING ARRAY INSTEAD OF THIS*/
        int nRows;
        int nCols;
        static ReaderWriterLock rwl;
        private static Semaphore _pool;
        static int readerTimeouts = 0;
        static int writerTimeouts = 0;
        static int reads = 0;
        static int writes = 0;
        static Mutex[] mutRows;
        static Mutex[] mutCols;



        public SharableSpreadaheet(int nRows1, int nCols1)
        {
            //The speardsheet starts at cell 1,1 (top, left) -> so we'll give 1 extra row and 1 extra col, and only use indices 1,2,3... (never 0)
            this.nRows = nRows1 + 1;
            this.nCols = nCols1 + 1;
            rwl = new ReaderWriterLock();
            _pool = null;
                

            // construct a nRows*nCols spreadsheet
            this.spreadsheet = new String[this.nRows][];
            for(int i = 0; i < nRows; i++)
            {
                this.spreadsheet[i] = new string[nCols];
            }
            //init spreadsheet
            initSpreadsheet();
        }

        public bool setConcurrentSearchLimit(int nUsers)
        {
            _pool = new Semaphore(0, nUsers);
            return true;
        }

        private void initSpreadsheet()
        {
            for (int i = 1; i < this.nRows; i++)
            {
                for (int j = 1; j < this.nCols; j++)
                {
                    //notice that we don't initialize ANY cell that has 0 index in its coordinates. That's on purpose!
                    String placeholder = "testcell" + i + j;

                    spreadsheet[i][j] = placeholder;//TODO:leave the init with placeholder string? 
                    Console.Out.WriteLine("{0}", spreadsheet[i][j]);
                }
            }
        }

        private bool isValidIndices(int row, int col)
        {
            if (row < this.nRows && col < this.nCols && row > 0 && col > 0)
            {
                return true;
            }
            return false;
        }   
        private bool isValidRowIndex(int row)
        {

            if (row < this.nRows && row > 0)
            {
                return true;
            }
            return false;
        }
        private bool isValidColIndex(int col)
        {
            if (col < this.nCols && col > 0)
            {
                return true;
            }
            return false;
        }

        public String getCell(int row, int col)//REQUIRED!
        {
            try
            {
                rwl.AcquireReaderLock(1000);
                try
                {

                    if (isValidIndices(row, col))
                    {
                        // return the string at [row,col]
                        String str_to_ret = this.spreadsheet[row][col];
                        return str_to_ret;
                    }
                }

                finally
                {
                    // Ensure that the lock is released.
                    rwl.ReleaseReaderLock();
                }
            }
            catch (ApplicationException)
            {
                // The reader lock request timed out.
                Interlocked.Increment(ref readerTimeouts);
            }
            return null;
        }
        
        
        //@@@@
        public bool setCell(int row, int col, String str)//REQUIRED!
        {
            try
            {
                rwl.AcquireWriterLock(1000);
                try
                {

                    if (isValidIndices(row, col))
                    {
                        this.spreadsheet[row][col] = str;
                        return true;
                    }
                    return false;
                }
                finally
                {
                    // Ensure that the lock is released.
                    rwl.ReleaseWriterLock();
                }
            }
            catch (ApplicationException)
            {
                // The reader lock request timed out.
                Interlocked.Increment(ref writerTimeouts);
            }
            return false;
        }


        public bool searchString(String str, ref int row, ref int col)//REQUIRED!
        {
            bool tmp;
            // return the first cell that contains the string (search from first row to the last row)
            if (_pool == null)
            {
                try
                {
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = helpSearch(str, ref row, ref col);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts) ;
                }

            }
            else
            {
                try
                {
                    _pool.WaitOne();
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = helpSearch(str, ref row, ref col);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                        _pool.Release();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }
            }
            return false;
             
        }

        private bool helpSearch(String str, ref int row, ref int col)
        {
            for (int i = 1; i < this.nRows; i++)
            {
                for (int j = 1; j < this.nCols; j++)
                {
                    if (Equals(str, this.spreadsheet[i][j]))
                    {
                        // stores the location in row,col.
                        row = i;
                        col = j;
                        return true;
                    }
                }
            }
            return false;
        }




        //@@
        public bool exchangeRows(int row1, int row2)//REQUIRED!
        {
            if (isValidRowIndex(row1) && isValidRowIndex(row2))
            {
                // exchange the content of row1 and row2
                for (int i = 1; i < this.nCols; i++)
                {
                    //foreach Cell[row1][1..nCols] & Cell[row2][1...nCols] -> swap string values 
                    String auxCellContent = this.spreadsheet[row1][i];
                    this.spreadsheet[row1][i] = this.spreadsheet[row2][i];
                    this.spreadsheet[row2][i] = auxCellContent;
                }
                return true;
            }
            return false;
        }
        //@@
        public bool exchangeCols(int col1, int col2)//REQUIRED!
        {

            try
            {
                rwl.AcquireWriterLock(1000);
                try
                {
                    if (isValidColIndex(col1) && isValidColIndex(col2))
                    {
                        // exchange the content of col1 and col2
                        for (int i = 1; i < this.nRows; i++)
                        {
                            //foreach Cell[row1][1..nCols] & Cell[row2][1...nCols]-> swap string values 
                            string auxCellContent = this.spreadsheet[i][col1];
                            this.spreadsheet[i][col1] = (this.spreadsheet[i][col2]);
                            this.spreadsheet[i][col2] = auxCellContent;
                        }
                        return true;
                    }
                    return false;

                }
                finally
                {
                    // Ensure that the lock is released.
                    rwl.ReleaseWriterLock();
                }
            }
            catch (ApplicationException)
            {
                // The reader lock request timed out.
                Interlocked.Increment(ref writerTimeouts);
            }
            return false;
 
        }







        public bool searchInRow(int row, String str, ref int col)//REQUIRED!
        {

            bool tmp;
            // return the first cell that contains the string (search from first row to the last row)
            if (_pool == null)
            {
                try
                {
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = searchrowHelper(row,str,ref col);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }

            }
            else
            {
                try
                {
                    _pool.WaitOne();
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = searchrowHelper(row, str, ref col);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                        _pool.Release();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }
            }
            return false;
       
        }


        private bool searchrowHelper(int row, String str, ref int col)
        {
                 if (isValidRowIndex(row))
            {
                // perform search in specific row
                for (int i = 1; i< this.nCols; i++)
                {
                    if (Equals(str, this.spreadsheet[row][i]))
                    {
            col = i;
            return true;
        }
        }
                return false;
            }
            return false;
        }



        public bool searchInCol(int col, String str, ref int row)//REQUIRED!
        {

            bool tmp;
            // return the first cell that contains the string (search from first row to the last row)
            if (_pool == null)
            {
                try
                {
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = searchcolHelper(col, str, ref row);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }

            }
            else
            {
                try
                {
                    _pool.WaitOne();
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = searchcolHelper(col, str, ref row);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                        _pool.Release();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }
            }
            return false;
        }


        private bool searchcolHelper(int col, String str, ref int row)
        {
            if (isValidColIndex(col))
            {
                // perform search in specific col
                for (int i = 1; i < this.nRows; i++)
                {
                    if (Equals(str, this.spreadsheet[i][col]))
                    {
                        row = i;
                        return true;
                    }
                }
                return false;
            }
            return false;
        }




        public bool searchInRange(int col1, int col2, int row1, int row2, String str, ref int row, ref int col)//REQUIRED!
        {
            bool tmp;
            // return the first cell that contains the string (search from first row to the last row)
            if (_pool == null)
            {
                try
                {
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = searchRangeHelper( col1,  col2,  row1,  row2,  str, ref  row, ref  col);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }

            }
            else
            {
                try
                {
                    _pool.WaitOne();
                    rwl.AcquireReaderLock(1000);
                    try
                    {
                        tmp = searchRangeHelper(col1, col2, row1, row2, str, ref row, ref col);
                        return tmp;
                    }
                    finally
                    {
                        rwl.ReleaseReaderLock();
                        _pool.Release();
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }
            }
            return false;
        }


        private bool searchRangeHelper(int col1, int col2, int row1, int row2, String str, ref int row, ref int col)
        {
            // perform search within spesific range: [row1:row2,col1:col2] 
            //includes col1,col2,row1,row2
            if (isValidIndices(row1, col1) && isValidIndices(row2, col2))
            {
                System.Console.WriteLine("Inside searchInRange, before swapping: r1={0} r2={1} ; c1={2} c2={3}", row1, row2, col1, col2);
                swapIf_1_isBiggerThan_2(ref row1, ref row2);
                swapIf_1_isBiggerThan_2(ref col1, ref col2);
                System.Console.WriteLine("Inside searchInRange, after swapping: r1={0} r2={1} ; c1={2} c2={3}", row1, row2, col1, col2);
                for (int i = row1; i <= row2; i++)
                {
                    for (int j = col1; j <= col2; j++)
                    {
                        if (Equals(str, this.spreadsheet[i][j]))
                        {
                            row = i;
                            col = j;
                            return true;
                        }
                    }
                }
            }
            return false;
        }



        private void swapIf_1_isBiggerThan_2(ref int first, ref int second)
        {
            if (first > second)
            {
                int aux = first;
                first = second;
                second = aux;
                System.Console.WriteLine("Inside swapIf_1_isBiggerThan_2: {0} {1}", first, second);
            }
        }


        //@@
        public bool addRow(int row1)//REQUIRED!
        {
            bool tmp;
            try
            {
                rwl.AcquireWriterLock(1000);
                try
                {
                    tmp = addRowHelper(row1);
                    return tmp;
                    
                }
                finally
                {
                    // Ensure that the lock is released.
                    rwl.ReleaseWriterLock();
                }
            }
            catch (ApplicationException)
            {
                // The reader lock request timed out.
                Interlocked.Increment(ref writerTimeouts);
            }
            return false;
        }
        private bool addRowHelper(int row1)
        {
            if (isValidRowIndex(row1))
            {
                //add a row after row1, i.e. make the spreadsheet at index row1+1 an empty new row.
                int old_nRows = spreadsheet.Length;
                this.nRows = old_nRows + 1;

                // Resize the array to a bigger size (1 element larger).
                Array.Resize(ref this.spreadsheet, old_nRows + 1);
                int lastRowIdx = spreadsheet.Length - 1;

                //shift all elements from row1 onwards: spreadsheet[row1+1] -> spreadsheet[row1+2], spreadsheet[row1+2] -> spreadsheet[row1+3] ... 
                for (int i = old_nRows - 1; i > row1; i--)
                {
                    spreadsheet[i + 1] = spreadsheet[i];
                }
                spreadsheet[row1 + 1] = new string[this.nCols];
                //init the Cells at new row (= spreadsheet[row1 + 1][1..nCols] ) 
                /*for (int i = 1; i < nCols; i++)
                {
                    //string placeholder = "testcell" + (row1 + 1) + i;
                    spreadsheet[row1 + 1][i] = "";
                }*/
                return true;
            }
            return false;
        }


        //@@
        public bool addCol(int col1)//REQUIRED!
        {
            bool tmp;
            try
            {
                rwl.AcquireWriterLock(1000);
                try
                {
                    tmp = addColHelper(col1);
                    return tmp;
                }
                finally
                {
                    // Ensure that the lock is released.
                    rwl.ReleaseWriterLock();
                }
            }
            catch (ApplicationException)
            {
                // The reader lock request timed out.
                Interlocked.Increment(ref writerTimeouts);
            }
            return false;
        }
        private bool addColHelper(int col1)
        {
            if (isValidColIndex(col1))
            {
                //add a col after col1, i.e. make the spreadsheet at index col1+1 an empty new col.
                int old_nCols = this.nCols;
                this.nCols = old_nCols + 1;
                // Resize each row by 1 column
                for (int i = 1; i < spreadsheet.Length; i++)
                {
                    Array.Resize(ref spreadsheet[i], old_nCols + 1);
                }

                //shift all elements from col1 onwards 
                for (int i = 1; i < spreadsheet.Length; i++)
                {
                    for (int j = old_nCols - 1; j > col1; j--)
                    {
                        spreadsheet[i][j + 1] = spreadsheet[i][j];
                    }
                }

                //init the Cells at new col (= spreadsheet[1...nRows][col1 + 1] ) 
                for (int i = 1; i < nRows; i++)
                {
                    String placeholder = "testcell" + i + (col1 + 1);
                    spreadsheet[i][col1 + 1] = placeholder;
                }
                return true;
            }
            return false;
        }




        public void getSize(ref int nRows, ref int nCols)//REQUIRED!
        {
            // return the size of the spreadsheet in nRows, nCols
            rwl.AcquireReaderLock(1000);
            nRows = this.nRows;
            nCols = this.nCols;
            rwl.ReleaseReaderLock();
        }

        

        public bool save(String fileName)//REQUIRED!
        {
            try
            {
                rwl.AcquireReaderLock(1000);
                try
                {
                    // save the spreadsheet to a file fileName.
                    // you can decide the format you save the data. There are several options.
                    // This text is added only once to the file.
                    using (var sw = new StreamWriter(fileName))
                    {
                        sw.WriteLine(this.nRows);
                        sw.WriteLine(this.nCols);
                        for (int i = 1; i < this.nRows; i++)
                        {
                            for (int j = 1; j < this.nCols; j++)
                            {
                                sw.Write(spreadsheet[i][j] + ",");
                            }
                            sw.Write("\n");
                        }
                        sw.Flush();
                        sw.Close();
                        return true;
                    }
                    return true;
                }
                finally { rwl.ReleaseReaderLock(); }
            }
            catch (ApplicationException)
            {
                // The reader lock request timed out.
                Interlocked.Increment(ref readerTimeouts);
                return false;
            }
        }   

        public bool load(String fileName)//REQUIRED!
        {
            try
            {
                rwl.AcquireReaderLock(1000);
                try
                {
                    using (var sr = new StreamReader(fileName))
                    {
                        int rows = int.Parse(sr.ReadLine());
                        int cols = int.Parse(sr.ReadLine());
                        this.spreadsheet = new string[rows][];
                        for (int i = 0; i < rows; i++)
                        {
                            this.spreadsheet[i] = new string[cols];
                        }

                        string[] line = new string[cols];
                        int k = 0;
                        for (int i = 1; i < rows; i++)
                        {
                            k = 0;
                            line = sr.ReadLine().TrimEnd(',').Split(',');
                            for (int j = 1; j < cols; j++)
                            {
                                this.spreadsheet[i][j] = line[k];

                                k++;
                            }
                        }

                        sr.Close();
                    }
                    return true;
                }
                finally { rwl.ReleaseReaderLock(); }
             }
            catch (ApplicationException)
            {
                // The reader lock request timed out.
                Interlocked.Increment(ref readerTimeouts);
                return false;
            }
        }
    }
}
    /*******************************************************************************************************************/
  