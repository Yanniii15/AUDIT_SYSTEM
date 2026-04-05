class Expense < ApplicationRecord
  # 🟢 FIX 1: Allow the expense to exist even if the user is deleted
  belongs_to :user, optional: true

  # SAFETY CHECK: Don't let them save if they don't have the cash
  validate :check_user_balance, on: :create

  # These are the "tripwires"
  after_create :deduct_from_user_wallet
  after_destroy :refund_to_user_wallet
  before_update :adjust_user_wallet_on_update

  private

  # 0. The Gatekeeper: Prevents negative balances
  def check_user_balance
    return unless user # 🟢 FIX 2: Safety check

    if (user.pcf_balance || 0) < self.amount
      errors.add(:amount, "exceeds your current wallet balance (₱#{user.pcf_balance || 0})")
    end
  end

  # 1. When a new expense is made
  def deduct_from_user_wallet
    return unless user # 🟢 FIX 2: Safety check

    new_balance = (user.pcf_balance || 0) - self.amount
    user.update_column(:pcf_balance, new_balance)
  end

  # 2. If an expense is deleted, give the money back
  def refund_to_user_wallet
    return unless user # 🟢 FIX 2: Safety check

    # Use self.amount here, amount_was is only for editing!
    new_balance = (user.pcf_balance || 0) + (self.amount || 0)
    user.update_column(:pcf_balance, new_balance)
  end

  # 3. If an expense is edited
  def adjust_user_wallet_on_update
    return unless user # 🟢 FIX 2: Safety check
    
    if amount_changed?
      diff = self.amount - self.amount_was
      new_balance = (user.pcf_balance || 0) - diff
      user.update_column(:pcf_balance, new_balance)
    end
  end
end