USE Eventizo27;
GO

ALTER TABLE PendingPaymentSeats
ALTER COLUMN SeatCode NVARCHAR(20) NOT NULL;

ALTER TABLE OccupiedSeats
ALTER COLUMN SeatCode NVARCHAR(20) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PendingSeat')
BEGIN
    CREATE UNIQUE INDEX UX_PendingSeat
    ON PendingPaymentSeats (EventId, SeatCode);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OccupiedSeat')
BEGIN
    CREATE UNIQUE INDEX UX_OccupiedSeat
    ON OccupiedSeats (EventId, SeatCode);
END
GO

-- 1️⃣ Xóa dữ liệu cũ TRƯỚC (để tránh lỗi khóa ngoại)
-- Xóa dữ liệu bảng Products và reset Identity
DELETE FROM Products;
DBCC CHECKIDENT ('Products', RESEED, 0); -- ID tiếp theo sẽ là 1

-- Xóa dữ liệu bảng Events và reset Identity
DELETE FROM Events;
DBCC CHECKIDENT ('Events', RESEED, 0); -- ID tiếp theo sẽ là 1
GO


-- 2️⃣ Đảm bảo cột AverageRating có DEFAULT 0 (chỉ chạy 1 lần)
IF NOT EXISTS (
    SELECT * FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID('Events') 
      AND name = 'DF_Events_AverageRating'
)
BEGIN
    ALTER TABLE Events
    ADD CONSTRAINT DF_Events_AverageRating DEFAULT 0 FOR AverageRating;
END
GO

-- 3️⃣ Thêm dữ liệu mẫu cho Categories
INSERT INTO Categories(Name) VALUES
(N'Lightstick'),
(N'Album');
GO

-- 4️⃣ Thêm dữ liệu mẫu cho EventTypes
INSERT INTO EventTypes(Name) VALUES
(N'Concert'),
(N'Tour'),
(N'Liveshow');
GO

-- 5️⃣ Thêm dữ liệu sự kiện (EventStartingDate + EventEndingDate)
SET DATEFORMAT DMY;

INSERT INTO Events(
    Name, Description, EventStartingDate, EventEndingDate, 
    Place, Status, Capacity, ImageUrl, EventTypeId, 
    PriceMin, PriceReducedMin, PriceMax, PriceReducedMax, TicketType
)
VALUES
-- 🔁 SỰ KIỆN ĐÃ QUA → ĐƯA VỀ TUẦN HIỆN TẠI (16–22/12/2025)

(N'Music Tour', N'Đêm nhạc ngoài trời sôi động.', '2025-12-16 18:00', '2025-12-16 23:59',
 N'Sân vận động Đà Lạt, 206A De Lattre de Tassigny', N'', 300, '/images/tour/4.png', 2, 300000, NULL, 500000, NULL, ''),

(N'Nắng nhẹ bên thềm', N'Không gian ấm cúng và nhẹ nhàng.', '2025-12-16 19:00', '2025-12-16 21:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/NNBT.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Mộng mơ trong âm nhạc', N'Hòa mình vào những giai điệu thư giãn.', '2025-12-17 17:00', '2025-12-17 20:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/MMTAN.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Lời ca giữa vì sao', N'Giai điệu nhẹ nhàng giữa trời đêm.', '2025-12-17 19:00', '2025-12-17 22:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/LCGNVS.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Music concert', N'Âm nhạc kết nối mọi người.', '2025-12-18 18:00', '2025-12-18 22:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/concert/concert1.png', 1, 300000, NULL, 500000, NULL, ''),

(N'Rise & Shine', N'Năng lượng âm nhạc bùng nổ.', '2025-12-18 19:00', '2025-12-18 22:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/Rise&Shine.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Music event', N'Không gian âm nhạc sôi động.', '2025-12-19 20:00', '2025-12-19 23:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/3.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Đêm thu', N'Giai điệu sâu lắng và trữ tình.', '2025-12-19 16:00', '2025-12-19 20:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/DT.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Night Party', N'Bữa tiệc âm nhạc đêm đầy năng lượng.', '2025-12-20 19:00', '2025-12-20 23:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/4.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Festival Music', N'Lễ hội âm nhạc đa sắc màu.', '2025-12-20 20:30', '2025-12-20 23:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/ps3.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Bứt phá', N'Âm nhạc mạnh mẽ và cuồng nhiệt.', '2025-12-21 20:00', '2025-12-21 23:30',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/BP.png', 3, 300000, NULL, 500000, NULL, ''),

-- ⏭️ SỰ KIỆN CHƯA TỚI → GIỮ NGUYÊN

(N'Dưới ánh đèn mờ', N'Không gian trầm lắng và cảm xúc.', '2025-12-21 20:00', '2025-12-21 23:30',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/lv5.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Galaxy Pulse 2025 - Day 1', N'Đêm EDM mở màn đầy bùng nổ.', '2025-12-22 18:00', '2025-12-22 23:59',
 N'Sân vận động Mỹ Đình', N'', 300, '/images/tour/1.png', 2, 300000, NULL, 500000, NULL, ''),

(N'Galaxy Pulse 2025 - Day 2', N'Âm nhạc tiếp nối hành trình cảm xúc.', '2025-12-23 18:00', '2025-12-23 23:59',
 N'Sân vận động Phú Thọ', N'', 300, '/images/tour/2.png', 2, 300000, NULL, 500000, NULL, ''),

(N'Summer Echo 2025', N'Mùa hè rực rỡ cùng âm nhạc.', '2025-12-25 18:30', '2025-12-25 23:00',
 N'Sân vận động Hòa Xuân, Đà Nẵng', N'', 300, '/images/tour/3.png', 2, 350000, NULL, 600000, NULL, ''),

(N'Symphony of Lights', N'Âm nhạc và ánh sáng hòa quyện.', '2025-12-26 18:00', '2025-12-26 22:00',
 N'180/26F Lạc Long Quân', N'', 300, '/images/concert/conert 1.png', 1, 300000, NULL, 500000, NULL, ''),

(N'City Beats', N'Nhịp sống đô thị trong âm nhạc.', '2025-12-27 19:00', '2025-12-27 22:30',
 N'Nhà hát Hòa Bình, TP.HCM', N'', 300, '/images/concert/conert 2.png', 1, 320000, NULL, 550000, NULL, ''),

(N'Starfall Night', N'Đêm nhạc lung linh dưới trời sao.', '2025-12-28 19:00', '2025-12-28 23:00',
 N'Sân khấu Lan Anh, TP.HCM', N'', 300, '/images/concert/conert 3.png', 1, 400000, NULL, 700000, NULL, ''),

(N'Bass & Blaze', N'Đêm EDM đầy năng lượng.', '2025-12-29 19:00', '2025-12-29 23:59',
 N'Sân vận động Cần Thơ', N'', 300, '/images/concert/conert 4.png', 1, 350000, NULL, 600000, NULL, ''),

(N'Melody at Dusk', N'Khoảnh khắc thư giãn cùng hoàng hôn.', '2025-12-30 17:00', '2025-12-30 21:30',
 N'180/26F Lạc Long Quân', N'', 300, '/images/liveshow/liveshow 1.png', 3, 300000, NULL, 500000, NULL, ''),

(N'Moon Serenade', N'Giai điệu trầm bổng nơi ánh trăng.', '2026-01-02 18:00', '2026-01-02 21:30',
 N'Nhà hát Lớn Hà Nội', N'', 300, '/images/liveshow/liveshow 2.png', 3, 300000, NULL, 550000, NULL, ''),

(N'Starlight Harmony', N'Đêm nhạc lãng mạn và sâu lắng.', '2026-01-05 19:00', '2026-01-05 22:00',
 N'Nhà hát Trưng Vương, Đà Nẵng', N'', 300, '/images/liveshow/liveshow 3.png', 3, 280000, NULL, 480000, NULL, ''), 

(N'Nhịp trái tim', N'Không gian acoustic gần gũi.', '2026-01-08 19:00', '2026-01-08 21:30',
 N'Nhà Văn hóa Thanh Niên, TP.HCM', N'', 300, '/images/liveshow/liveshow 4.png', 3, 250000, NULL, 400000, NULL, '');
GO

-- 6️⃣ Thêm sản phẩm mẫu
INSERT INTO Products
(
    Name,
    Price,
    PriceReduced,
    Status,
    Quantity,
    Description,
    ImageUrl,
    CategoryId
)
VALUES
(N'Light stick khủng long hồng', 500000, NULL, N'Available', 100,
 N'Lightstick "khủng long hồng" có thiết kế độc đáo...',
 '/images/LSH.jpg', 1),

(N'Light stick khủng long đen', 800000, NULL, N'Available', 200,
 N'Lightstick "khủng long đen" có thiết kế độc đáo...',
 '/images/LSD.jpg', 1),

(N'Album Stage on fire', 1200000, NULL, N'Available', 59,
 N'Album "Stage on Fire" là một sản phẩm âm nhạc...',
 '/images/album.png', 2);

GO

