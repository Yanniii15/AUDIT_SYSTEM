class ExpensesController < ApplicationController
  before_action :set_expense, only: %i[ show edit update destroy ]

  # GET /expenses or /expenses.json
def index
    # 1. ROLE-BASED ACCESS CONTROL (RBAC) LOGIC
    if current_user.admin?
      @expenses = Expense.all.order(entry_date: :desc)
    elsif current_user.co_admin?
      staff_ids = current_user.staff_members.pluck(:id)
      allowed_ids = [current_user.id] + staff_ids
      @expenses = Expense.where(user_id: allowed_ids).order(entry_date: :desc)
    else
      @expenses = current_user.expenses.order(entry_date: :desc)
    end

    # 2. CALCULATE TODAY'S SPENDING
    @today_expenses = @expenses.where(entry_date: Date.today)
    @total_spent_today = @today_expenses.sum(:amount)

    # 3. MATH LOGIC FIX: WALLET vs FIXED FLOAT
    if current_user.admin?
      @current_wallet = User.sum(:pcf_balance) || 0
      @starting_float = User.sum(:daily_starting_float) || 0
      
      # ✨ NEW: Step 4 - For Admin "Cash Distribution" View
      # This grabs everyone else's name and their current wallet balance
      @staff_balances = User.where.not(role: 'admin').order(:role)

    elsif current_user.co_admin?
      staff_ids = current_user.staff_members.pluck(:id)
      allowed_ids = [current_user.id] + staff_ids
      @current_wallet = User.where(id: allowed_ids).sum(:pcf_balance) || 0
      @starting_float = User.where(id: allowed_ids).sum(:daily_starting_float) || 0
    else
      @current_wallet = current_user.pcf_balance || 0 
      @starting_float = current_user.daily_starting_float || 0
    end
  end

  # GET /expenses/1 or /expenses/1.json
  def show
  end

  # GET /expenses/new
  def new
    @expense = Expense.new
  end

  def report
    # Capture dates from the form, or default to the beginning of the month
    @start_date = params[:start_date].presence || Date.today.beginning_of_month.to_s
    @end_date = params[:end_date].presence || Date.today.to_s

    @expenses = Expense.where(entry_date: @start_date..@end_date).order(entry_date: :desc)
    @total_amount = @expenses.sum(:amount)
  end

  # GET /expenses/1/edit
  def edit
  end

  # POST /expenses or /expenses.json
  def create
    @expense = Expense.new(expense_params)
    @expense.user = current_user # Assuming you have this set up

    respond_to do |format|
      if @expense.save
        # CHANGED THIS LINE:
        format.html { redirect_to root_path, notice: "Expense was successfully created." }
        format.json { render :show, status: :created, location: @expense }
      else
        format.html { render :new, status: :unprocessable_entity }
        format.json { render json: @expense.errors, status: :unprocessable_entity }
      end
    end
  end

  # PATCH/PUT /expenses/1 or /expenses/1.json
  def update
    respond_to do |format|
      if @expense.update(expense_params)
        # CHANGED THIS LINE TOO:
        format.html { redirect_to root_path, notice: "Expense was successfully updated." }
        format.json { render :show, status: :ok, location: @expense }
      else
        format.html { render :edit, status: :unprocessable_entity }
        format.json { render json: @expense.errors, status: :unprocessable_entity }
      end
    end
  end

  # DELETE /expenses/1 or /expenses/1.json
  def destroy
    @expense.destroy!

    respond_to do |format|
      format.html { redirect_to expenses_path, notice: "Expense was successfully destroyed.", status: :see_other }
      format.json { head :no_content }
    end
  end

  private
    # Use callbacks to share common setup or constraints between actions.
    def set_expense
      @expense = Expense.find(params.expect(:id))
    end

    # Only allow a list of trusted parameters through.
    def expense_params
      params.expect(expense: [ :entry_date, :description, :amount, :department, :pcf_source, :is_verified, :notes, :receipt_photo ])
    end
end