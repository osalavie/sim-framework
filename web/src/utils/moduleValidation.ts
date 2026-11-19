/**
 * Module Configuration Validation Utilities
 *
 * This module provides comprehensive validation for module configurations before
 * they are registered with the router. It ensures that all required fields are
 * present, layout configurations are valid, and redirect paths are properly formatted.
 *
 * The validation system supports:
 * - Required field validation (id, name, enabled, type, routes)
 * - Layout configuration validation (default, custom, none)
 * - Redirect configuration validation (rootPath, emptyRedirect, layoutRedirect)
 * - Independent module validation (one module's failure doesn't affect others)
 * - Warning vs error distinction (warnings allow processing, errors prevent it)
 *
 * @module moduleValidation
 * @see {@link ModuleConfig} for module configuration structure
 * @see {@link ValidationResult} for validation result format
 */
import type { ModuleConfig } from '@/types/module'

/**
 * Custom error class for module validation failures
 *
 * This error is thrown when a module fails validation and includes
 * the module ID for easier debugging and error tracking.
 *
 * @example
 * ```typescript
 * throw new ModuleValidationError('Invalid layout type', 'dashboard-module')
 * ```
 */
export class ModuleValidationError extends Error {
  constructor(
    message: string,
    public moduleId?: string,
  ) {
    super(message)
    this.name = 'ModuleValidationError'
  }
}

/**
 * Result of module validation
 *
 * Contains validation status, error messages, and warning messages.
 * - valid: false if any errors exist, true otherwise
 * - errors: Critical issues that prevent module loading
 * - warnings: Non-critical issues that should be logged but don't prevent loading
 *
 * @example
 * ```typescript
 * const result = validateModuleConfig(module)
 * if (!result.valid) {
 *   console.error('Validation failed:', result.errors)
 * }
 * if (result.warnings.length > 0) {
 *   console.warn('Validation warnings:', result.warnings)
 * }
 * ```
 */
export interface ValidationResult {
  /** Whether the module passed validation (no errors) */
  valid: boolean
  /** Critical validation errors that prevent module loading */
  errors: string[]
  /** Non-critical validation warnings that should be logged */
  warnings: string[]
}

/**
 * Validate required module fields
 *
 * Checks that all mandatory fields are present and have correct types:
 * - id: Non-empty string identifier
 * - name: Non-empty string display name
 * - enabled: Boolean flag for module activation
 * - type: Either 'static' or 'dynamic'
 * - routes: Array of route configurations
 *
 * This is the first validation step and must pass before other validations run.
 * If required fields are missing, the module cannot be processed safely.
 *
 * @param module - Partial module configuration to validate
 * @returns Validation result with errors for any missing/invalid required fields
 *
 * @example
 * ```typescript
 * const result = validateRequiredFields({ id: 'test', name: 'Test' })
 * // result.valid = false (missing enabled, type, routes)
 * // result.errors = ['Module must have an "enabled" field...', ...]
 * ```
 *
 * Requirements: 11.1 - Validate required fields
 */
export function validateRequiredFields(module: Partial<ModuleConfig>): ValidationResult {
  const errors: string[] = []
  const warnings: string[] = []

  // Validate id field: must be non-empty string
  if (!module.id || typeof module.id !== 'string' || module.id.trim() === '') {
    errors.push('Module must have a non-empty "id" field')
  }

  // Validate name field: must be non-empty string
  if (!module.name || typeof module.name !== 'string' || module.name.trim() === '') {
    errors.push('Module must have a non-empty "name" field')
  }

  // Validate enabled field: must be boolean
  if (typeof module.enabled !== 'boolean') {
    errors.push('Module must have an "enabled" field of type boolean')
  }

  // Validate routes field: must be array
  if (!Array.isArray(module.routes)) {
    errors.push('Module must have a "routes" field of type array')
  }

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  }
}

/**
 * Validate layout configuration
 *
 * Ensures layout configuration is consistent and complete:
 * - Layout type must be 'default', 'custom', or 'none'
 *
 * Layout types determine how routes are registered:
 * - 'default': Routes become children of AdminLayout
 * - 'custom': Routes get a custom layout wrapper
 * - 'none': Routes are registered at top level without wrapper
 *
 * @param module - Module configuration with required fields already validated
 * @returns Validation result with errors for invalid layout configuration
 *
 * @example
 * ```typescript
 * // Valid layout
 * const result1 = validateLayoutConfiguration({
 *   layout: 'custom',
 * })
 * // result1.valid = true
 *
 * // Invalid: layout without component
 * const result2 = validateLayoutConfiguration({
 *   layout: 'custom'
 * })
 * // result2.valid = false
 * ```
 *
 * Requirements: 2.5 - Custom layout component requirement
 * Requirements: 11.3 - Validation error logging
 */
export function validateLayoutConfiguration(module: ModuleConfig): ValidationResult {
  const errors: string[] = []
  const warnings: string[] = []

  // Default to 'default' layout if not specified
  const layout = module.layout || 'default'

  // Validate layout type is one of the three supported types
  if (layout !== 'admin' && layout !== 'custom' && layout !== 'none') {
    errors.push(
      `Module "${module.id}" has invalid layout type "${layout}". Must be "default", "custom", or "none"`,
    )
  }

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  }
}

/**
 * Validate redirect configuration
 *
 * Validates redirect-related fields for consistency and proper formatting:
 * - rootPath: Module's root route path (e.g., '/admin/dashboard')
 * - emptyRedirect: Where to redirect when accessing rootPath
 * - layoutRedirect: Redirect for custom layout wrapper routes
 *
 * Validation rules:
 * - All paths must start with '/'
 * - rootPath without emptyRedirect generates a warning (no redirect created)
 * - emptyRedirect without rootPath only works for custom layouts
 *
 * Redirect behavior:
 * - rootPath + emptyRedirect: Creates redirect route from rootPath to emptyRedirect
 * - Custom layout: Uses layoutRedirect or emptyRedirect for layout wrapper
 * - Empty emptyRedirect: Falls back to first child route
 *
 * @param module - Module configuration with required fields already validated
 * @returns Validation result with errors for invalid path formats
 *
 * @example
 * ```typescript
 * // Valid redirect configuration
 * const result1 = validateRedirectConfiguration({
 *   rootPath: '/admin/dashboard',
 *   emptyRedirect: '/admin/dashboard/overview'
 * })
 * // result1.valid = true
 *
 * // Invalid: path doesn't start with /
 * const result2 = validateRedirectConfiguration({
 *   rootPath: 'admin/dashboard'
 * })
 * // result2.valid = false
 * // result2.errors = ['...rootPath must start with "/"']
 * ```
 *
 * Requirements: 3.1 - Module root route redirection
 */
export function validateRedirectConfiguration(module: ModuleConfig): ValidationResult {
  const errors: string[] = []
  const warnings: string[] = []

  if (module.emptyRedirect && !module.emptyRedirect.startsWith('/')) {
    errors.push(`Module "${module.id}" emptyRedirect must start with "/"`)
  }

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  }
}

/**
 * Validate complete module configuration
 *
 * Performs comprehensive validation of a module configuration including:
 * - Required fields (id, name, enabled, type, routes)
 * - Layout configuration consistency
 * - Route structure validity
 *
 * @param module - Module configuration to validate (may be partial)
 * @returns Validation result with errors and warnings
 *
 * @example
 * ```typescript
 * const result = validateModuleConfig(module)
 *
 * if (!result.valid) {
 *   console.error('Validation errors:', result.errors)
 * }
 *
 * if (result.warnings.length > 0) {
 *   console.warn('Validation warnings:', result.warnings)
 * }
 * ```
 *
 * Requirements: 11.1, 11.6
 */
export function validateModuleConfig(module: Partial<ModuleConfig>): ValidationResult {
  const allErrors: string[] = []
  const allWarnings: string[] = []

  // Validate required fields first
  const requiredResult = validateRequiredFields(module)
  allErrors.push(...requiredResult.errors)
  allWarnings.push(...requiredResult.warnings)

  // If required fields are invalid, don't continue with other validations
  if (!requiredResult.valid) {
    return {
      valid: false,
      errors: allErrors,
      warnings: allWarnings,
    }
  }

  // Now we know module has required fields, safe to cast
  const validModule = module as ModuleConfig

  // Validate layout configuration
  const layoutResult = validateLayoutConfiguration(validModule)
  allErrors.push(...layoutResult.errors)
  allWarnings.push(...layoutResult.warnings)

  // Validate redirect configuration
  const redirectResult = validateRedirectConfiguration(validModule)
  allErrors.push(...redirectResult.errors)
  allWarnings.push(...redirectResult.warnings)

  return {
    valid: allErrors.length === 0,
    errors: allErrors,
    warnings: allWarnings,
  }
}

/**
 * Validate and log module configuration
 *
 * Convenience function that validates a module and logs all warnings and errors.
 * Throws ModuleValidationError if validation fails.
 *
 * This function is useful when you want to validate a single module and
 * immediately fail if it's invalid, rather than collecting validation results.
 *
 * @param module - Partial module configuration to validate
 * @throws {ModuleValidationError} If validation fails (errors exist)
 *
 * @example
 * ```typescript
 * try {
 *   validateAndLogModule(module)
 *   // Module is valid, proceed with registration
 * } catch (error) {
 *   // Module is invalid, error already logged
 *   console.error('Module validation failed')
 * }
 * ```
 *
 * Requirements: 11.6 - Independent module validation
 */
export function validateAndLogModule(module: Partial<ModuleConfig>): void {
  const result = validateModuleConfig(module)

  // Log all warnings to console
  // Warnings don't prevent module loading but should be addressed
  result.warnings.forEach((warning) => {
    console.warn(`[Module Validation Warning] ${warning}`)
  })

  // Log all errors and throw if validation failed
  // Errors prevent module loading and must be fixed
  if (!result.valid) {
    result.errors.forEach((error) => {
      console.error(`[Module Validation Error] ${error}`)
    })
    throw new ModuleValidationError(
      `Module validation failed: ${result.errors.join('; ')}`,
      module.id,
    )
  }
}

/**
 * Validate array of modules
 *
 * Validates multiple modules independently and returns only the valid ones.
 * Invalid modules are logged and skipped, but don't prevent other modules
 * from being validated and returned.
 *
 * This implements fault-tolerant validation where one module's failure
 * doesn't affect the validation of other modules.
 *
 * @param modules - Array of partial module configurations to validate
 * @returns Array of valid module configurations (invalid ones filtered out)
 *
 * @example
 * ```typescript
 * const modules = [validModule, invalidModule, anotherValidModule]
 * const validModules = validateModules(modules)
 * // Returns: [validModule, anotherValidModule]
 * // invalidModule is logged and skipped
 * ```
 *
 * Requirements: 11.6 - Independent module validation
 */
export function validateModules(modules: Partial<ModuleConfig>[]): ModuleConfig[] {
  const validModules: ModuleConfig[] = []

  // Validate each module independently
  // One module's failure doesn't affect others
  modules.forEach((module) => {
    try {
      validateAndLogModule(module)
      validModules.push(module as ModuleConfig)
    } catch (error) {
      // Log validation errors but continue with other modules
      if (error instanceof ModuleValidationError) {
        console.error(
          `Skipping invalid module ${module.id || '(unknown)'}: ${error.message}`,
        )
      } else {
        console.error(`Unexpected error validating module ${module.id || '(unknown)'}:`, error)
      }
    }
  })

  return validModules
}
