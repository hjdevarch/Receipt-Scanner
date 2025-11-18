use ReceiptScannerDB_Dev
GO

select * from Receipts order by SerialId 

Select COUNT(*) from Receipts(nolock)


select * from AspNetUsers

select Top (1000) * from ReceiptItems

-- udate first 80% of Receipts set UserId = 'd3b95bf5-4c0e-46d0-9753-1acf3df0f44d'
-- delete 80 percent from Receipts

;WITH CTE AS (
    SELECT TOP 90 PERCENT *
    FROM Receipts
)
UPDATE CTE
SET UserId = 'd3b95bf5-4c0e-46d0-9753-1acf3df0f44d';



select COUNT(*) from Receipts where userid = '77b68fa6-5277-457d-8cbd-5d15645903ce'

select * from ReceiptItems ri

select 
    Max(r.serialId)
from 
    receipts r 
where 
    r.UserId = '521e94e3-f675-41ca-8cf1-bb88d5692be5'

select 
    *
from 
    receipts r JOIN
    ReceiptItems ri ON r.Id = ri.ReceiptId
where 
    r.UserId = '521e94e3-f675-41ca-8cf1-bb88d5692be5'
    and r.SerialId>=1104-10
order by r.SerialId desc