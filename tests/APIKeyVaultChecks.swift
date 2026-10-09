import Foundation

enum APIKeyVaultChecks {
    static func run() throws {
        let suiteName = "EchoAPIKeyVaultChecks.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suiteName)!
        let vault = APIKeyVault(service: "local.echo.subtitle.tests.\(UUID().uuidString)")
        defer {
            for account in APIKeyAccount.allCases { try? vault.delete(account) }
            defaults.removePersistentDomain(forName: suiteName)
        }

        let missing = try vault.read(.soniox)
        precondition(missing == nil)
        try vault.store("soniox-fixture", for: .soniox)
        let firstValue = try vault.read(.soniox)
        precondition(firstValue == "soniox-fixture")
        try vault.store("updated-fixture", for: .soniox)
        let updatedValue = try vault.read(.soniox)
        precondition(updatedValue == "updated-fixture")
        defaults.set("stale-legacy-fixture", forKey: APIKeyAccount.soniox.rawValue)
        let securePrecedence = vault.load(.soniox, migratingFrom: defaults)
        precondition(securePrecedence.value == "updated-fixture" && securePrecedence.warning == nil)
        precondition(defaults.string(forKey: APIKeyAccount.soniox.rawValue) == nil)

        defaults.set("legacy-deepseek-fixture", forKey: APIKeyAccount.deepSeek.rawValue)
        let migration = vault.load(.deepSeek, migratingFrom: defaults)
        let migratedValue = try vault.read(.deepSeek)
        precondition(migration.value == "legacy-deepseek-fixture" && migration.warning == nil)
        precondition(migratedValue == "legacy-deepseek-fixture")
        precondition(defaults.string(forKey: APIKeyAccount.deepSeek.rawValue) == nil)

        try vault.save("  replacement-fixture  ", for: .deepSeek, defaults: defaults)
        let savedValue = try vault.read(.deepSeek)
        precondition(savedValue == "replacement-fixture")
        try vault.save(" \n ", for: .deepSeek, defaults: defaults)
        let deletedValue = try vault.read(.deepSeek)
        precondition(deletedValue == nil)

        try vault.delete(.soniox)
        let clearedValue = try vault.read(.soniox)
        precondition(clearedValue == nil)
        print("PASS: Keychain credential create/update/read/delete and verified legacy migration")
    }
}
