# Establishment Verification System Design Spec

This document details the additions and changes required to implement the intermediate Branch/Establishment Verification workflow in the PCF Auditing System.

---

## 1. Domain Model Changes

We will update the `Users` and `AuditItem` models to support the new `BranchStaff` role and sequential status states.

### `Users` Table Modifications
* **`UserRole` Enum**: Add `BranchStaff` role.
* **`EstablishmentId`**: Add a nullable foreign key `EstablishmentId` pointing to `Establishments(Id)`.
  * Mandatory when `Role == UserRole.BranchStaff`.
  * Null for `Owner`, `Manager`, and `Buyer` roles.

### `AuditStatus` Enum Modifications
Replace `Pending` with the following distinct sequential states:
1. `AwaitingBranchVerification`: Receipt is uploaded. Branch staff must verify physical items arrived at the location.
2. `AwaitingManagerApproval`: Branch staff verified the items. Manager/Owner must approve the audit.
3. `Approved`: Audit is fully approved and completed.
4. `Rejected`: Audit was rejected at either stage and the funds were refunded to the buyer.

---

## 2. User Registration Module Enhancements (Owner View)

The registration form (`Account/Register.cshtml`) will be updated to handle the `BranchStaff` role:
* **Conditional Dropdown**:
  * If `Role == BranchStaff`, show an **"Assigned Branch"** dropdown containing establishments. Hide the "Reporting Manager" dropdown.
  * If `Role == Buyer`, show the "Reporting Manager" dropdown and hide the "Assigned Branch" dropdown.
  * For other roles, hide both.
* **AccountController Save Logic**:
  * Set `User.EstablishmentId = model.EstablishmentId` only if `model.Role == UserRole.BranchStaff`.

---

## 3. Sequential Verification Workflow

### Step 1: Upload (Buyer)
* The Buyer uploads an invoice and details.
* The system deducts the amount from `Buyer.PcfBalance`.
* The `AuditItem` is saved with status `AwaitingBranchVerification`.

### Step 2: Branch Verification (BranchStaff)
* **Access Rules**: Users in the `BranchStaff` role can only access their branch's pending verifications.
* **VerifyList (Branch view)**:
  * Lists audits where `Status == AwaitingBranchVerification` and `EstablishmentId == CurrentUser.EstablishmentId`.
  * Clicking "Verify Received" sets status to `AwaitingManagerApproval`.
  * Clicking "Reject" sets status to `Rejected` and refunds the buyer's balance.

### Step 3: Manager Approval (Manager / Owner)
* **Access Rules**: Managers see audits where `Status == AwaitingManagerApproval` and `Buyer.ManagerId == Manager.Id`. Owners see all.
* **VerifyList (Manager view)**:
  * Lists audits in `AwaitingManagerApproval` state.
  * Clicking "Approve" sets status to `Approved`.
  * Clicking "Reject" sets status to `Rejected` and refunds the buyer's balance.

---

## 4. Dashboard & Query Changes

* **HomeController.Index Dashboard**:
  * `BranchStaff` users are redirected to their branch verification page or see only audits for their assigned establishment.
  * Filters updated to include `AwaitingBranchVerification` and `AwaitingManagerApproval` in the Status dropdown.
  * Calculations for dynamic totals include both pending states.
