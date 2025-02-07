# CSV to MySQL Importer

This .NET console application reads user data from a CSV file and inserts it into a MySQL database. The application ensures the table exists before inserting data, using `Dapper` for efficient database operations.

## 📌 Features
- Reads user data from a CSV file (`Name`, `Email`, `CreatedAt`).
- Inserts data into a MySQL database.
- Inserts data into a Cosmos DB database.
- Ensures the required table exists before inserting.
- Supports duplicate handling with `ON DUPLICATE KEY UPDATE`.

---

## 🚀 Prerequisites

Before running the application, ensure you have:

- **.NET 8 SDK** (Download: [https://dotnet.microsoft.com/en-us/download](https://dotnet.microsoft.com/en-us/download))
- **MySQL Server** installed and running
- **Cosmos DB** account with a database and collection
- **CSV file** formatted as:
  ```csv
  Name,Email,CreatedAt
  John Doe,johndoe@example.com,2024-02-06 12:30:00
  Jane Smith,janesmith@example.com,2024-02-05 15:45:00

```bash
git clone git@github.com:benhalverson/csv-to-db.git
cd csv-to-db
```

```bash
dotnet restore
```

## ⚙️ Configuration
```bash
cp appsettings.json.example appsettings.json
```

## 🚦 Running the Application
```bash
Usage: dotnet run --db=<mysql|cosmos> --data=<path-to-csv>
```