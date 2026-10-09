import Foundation
import Security

enum APIKeyAccount: String, CaseIterable, Equatable {
    case soniox = "sonioxAPIKey"
    case deepSeek = "deepSeekAPIKey"
}

struct APIKeyLoadResult {
    let value: String
    let warning: String?
}

enum APIKeyVaultError: Error {
    case keychain(OSStatus)
    case invalidValue
}

/// Stores provider credentials in the user's macOS Keychain. A legacy
/// UserDefaults value is removed only after the Keychain write can be read
/// back, so an interrupted migration leaves the previous value recoverable.
struct APIKeyVault {
    static let standard = APIKeyVault(service: "local.echo.subtitle.credentials")

    private let service: String

    init(service: String) {
        self.service = service
    }

    func read(_ account: APIKeyAccount) throws -> String? {
        var query = baseQuery(for: account)
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne

        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        guard status != errSecItemNotFound else { return nil }
        guard status == errSecSuccess else { throw APIKeyVaultError.keychain(status) }
        guard let data = result as? Data,
              let value = String(data: data, encoding: .utf8) else {
            throw APIKeyVaultError.invalidValue
        }
        return value
    }

    func store(_ value: String, for account: APIKeyAccount) throws {
        guard let data = value.data(using: .utf8) else { throw APIKeyVaultError.invalidValue }
        let query = baseQuery(for: account)
        let attributes: [String: Any] = [kSecValueData as String: data]

        let updateStatus = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        if updateStatus == errSecSuccess { return }
        guard updateStatus == errSecItemNotFound else {
            throw APIKeyVaultError.keychain(updateStatus)
        }

        var addQuery = query
        attributes.forEach { addQuery[$0.key] = $0.value }
        let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
        guard addStatus == errSecSuccess else { throw APIKeyVaultError.keychain(addStatus) }
    }

    func delete(_ account: APIKeyAccount) throws {
        let status = SecItemDelete(baseQuery(for: account) as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw APIKeyVaultError.keychain(status)
        }
    }

    func load(_ account: APIKeyAccount, migratingFrom defaults: UserDefaults) -> APIKeyLoadResult {
        let legacyValue = defaults.string(forKey: account.rawValue) ?? ""
        do {
            if let secureValue = try read(account) {
                defaults.removeObject(forKey: account.rawValue)
                return APIKeyLoadResult(value: secureValue, warning: nil)
            }

            guard !legacyValue.isEmpty else {
                return APIKeyLoadResult(value: "", warning: nil)
            }

            do {
                try store(legacyValue, for: account)
                guard try read(account) == legacyValue else { throw APIKeyVaultError.invalidValue }
                defaults.removeObject(forKey: account.rawValue)
                return APIKeyLoadResult(value: legacyValue, warning: nil)
            } catch {
                return APIKeyLoadResult(
                    value: legacyValue,
                    warning: "旧版设置中的 \(account == .soniox ? "Soniox" : "DeepSeek") Key 尚未迁入钥匙串，仍保留原值；请稍后重试保存。"
                )
            }
        } catch {
            let warning = legacyValue.isEmpty
                ? "macOS 钥匙串暂不可用，该 Key 尚未保存。"
                : "无法访问 macOS 钥匙串，旧版设置中的 Key 已保留；请重新保存设置。"
            return APIKeyLoadResult(
                value: legacyValue,
                warning: warning
            )
        }
    }

    func save(_ value: String, for account: APIKeyAccount, defaults: UserDefaults) throws {
        let trimmedValue = value.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmedValue.isEmpty {
            try delete(account)
            defaults.removeObject(forKey: account.rawValue)
        } else {
            try store(trimmedValue, for: account)
            defaults.removeObject(forKey: account.rawValue)
        }
    }

    private func baseQuery(for account: APIKeyAccount) -> [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            // Without kSecUseDataProtectionKeychain, SecItem uses the
            // traditional per-user macOS login keychain.
            kSecAttrService as String: service,
            kSecAttrAccount as String: account.rawValue
        ]
    }
}
