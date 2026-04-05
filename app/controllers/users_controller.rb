class UsersController < ApplicationController
  before_action :authenticate_user!
  
  # 🔒 NEW: Protect the edit/delete actions so only admins/co-admins can use them
  before_action :authorize_admin!, only: [:edit, :update, :destroy]

  def index
    if current_user.admin?
      # This allows the Admin to see themselves and use their own row as the "Bank"
      @staff_members = User.all.order(:role)
      @managers = User.where(role: ["admin", "co_admin"])
    elsif current_user.co_admin?
      @staff_members = current_user.staff_members
    else
      redirect_to root_path, alert: "You are not authorized to view this page."
    end
  end

  def add_pcf
    @staff = User.find(params[:id])
    
    # 1. Check which button was clicked!
    raw_amount = params[:amount].to_f 
    amount = params[:commit] == "- Subtract" ? -raw_amount : raw_amount

    if amount != 0
      manager = current_user
      
      ActiveRecord::Base.transaction do
        # 🟢 NEW: Check if Auntie is adding money to herself (Master Vault Deposit)
        is_self_transfer = (@staff.id == manager.id)

        # 2. Update Receiving Balance (Staff or Auntie herself)
        new_staff_balance = (@staff.pcf_balance || 0) + amount
        
        if new_staff_balance < 0
          redirect_to manage_staff_path, alert: "Error: Not enough funds."
          raise ActiveRecord::Rollback
        end

        # 3. Update Sending Balance (ONLY if giving money to someone else)
        if !is_self_transfer
          new_manager_balance = (manager.pcf_balance || 0) - amount
          
          # Manager can't give what they don't have
          if amount > 0 && new_manager_balance < 0
            redirect_to manage_staff_path, alert: "Error: You don't have enough funds."
            raise ActiveRecord::Rollback
          end

          # Deduct from Manager's wallet and Manager's Float
          manager.update_column(:pcf_balance, new_manager_balance)
          new_manager_float = (manager.daily_starting_float || 0) - amount
          manager.update_column(:daily_starting_float, new_manager_float)
        end

        # 4. Save receiving balance and sync the Float
        @staff.update_column(:pcf_balance, new_staff_balance)
        new_staff_float = (@staff.daily_starting_float || 0) + amount
        @staff.update_column(:daily_starting_float, new_staff_float)

        # 5. Success Message logic
        if is_self_transfer && amount > 0
          notice_msg = "💰 Master Vault funded with ₱#{amount}!"
        else
          action_word = amount > 0 ? "added to" : "subtracted from"
          notice_msg = "Successfully #{action_word} #{@staff.email}."
        end

        redirect_to manage_staff_path, notice: notice_msg
      end
    else
      redirect_to manage_staff_path, alert: "Please enter a valid amount."
    end
  end

  def assign_manager
    @staff = User.find(params[:id])
    
    if @staff.update(manager_id: params[:manager_id])
      redirect_to manage_staff_path, notice: "Successfully updated manager for #{@staff.email}."
    else
      redirect_to manage_staff_path, alert: "Failed to assign manager."
    end
  end

  # ==========================================
  # 🛠️ NEW METHODS FOR MANAGING ACCOUNTS
  # ==========================================

  def edit
    @user = User.find(params[:id])
  end

  def update
    @user = User.find(params[:id])
    if @user.update(user_params)
      # I am assuming 'manage_staff_path' is where your users index lives!
      redirect_to manage_staff_path, notice: "Account updated successfully."
    else
      render :edit, status: :unprocessable_entity
    end
  end

  def destroy
    @user = User.find(params[:id])
    @user.destroy
    redirect_to manage_staff_path, notice: "Account was successfully deleted."
  end

  private

  # 🔒 Security check to prevent basic staff from editing/deleting accounts
  def authorize_admin!
    unless current_user.admin? || current_user.co_admin? 
      redirect_to root_path, alert: "Access Denied: You do not have permission to view this page."
    end
  end

  # ✅ Whitelist the data we allow the admin to edit
  def user_params
    params.require(:user).permit(:name, :email, :role, :manager_id)
  end
end